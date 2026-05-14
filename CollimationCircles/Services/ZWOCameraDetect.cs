using CollimationCircles.Models;
using CommunityToolkit.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    /// <summary>
    /// Detects and manages ZWO ASI cameras using the native SDK
    /// </summary>
    internal class ZWOCameraDetect : ICameraDetect
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public Dictionary<ControlType, object> ControlMapping => new()
        {
            { ControlType.Gain, (int)ZWOASICameraInterop.ASI_CONTROL_TYPE.ASI_GAIN },
            { ControlType.ExposureTime, (int)ZWOASICameraInterop.ASI_CONTROL_TYPE.ASI_EXPOSURE }
        };

        public async Task<List<Camera>> GetCameras()
        {
            List<Camera> cameras = [];

            try
            {
                int numCameras = ZWOASICameraInterop.ASIGetNumOfConnectedCameras();
                logger.Info($"ASI SDK detected {numCameras} connected ZWO cameras");

                if (numCameras <= 0)
                {
                    logger.Info("No ZWO cameras detected");
                    return cameras;
                }

                for (int i = 0; i < numCameras; i++)
                {
                    try
                    {
                        var cameraInfo = new ZWOASICameraInterop.ASI_CAMERA_INFO();
                        int result = ZWOASICameraInterop.ASIGetCameraProperty(ref cameraInfo, i);

                        if (result == (int)ZWOASICameraInterop.ASI_ERROR_CODE.ASI_SUCCESS)
                        {
                            string cameraName = ByteArrayToString(cameraInfo.Name);
                            
                            Camera camera = new()
                            {
                                Index = i,
                                APIType = APIType.Zwo,
                                Name = cameraName,
                                Path = cameraInfo.CameraID.ToString()
                            };

                            camera.Controls = await GetControls(camera);
                            cameras.Add(camera);

                            logger.Info($"Added ZWO camera: '{camera.Name}' (ID: {cameraInfo.CameraID}, Index: {i})");
                        }
                        else
                        {
                            logger.Warn($"Failed to get property for camera index {i}, error code: {result}");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex, $"Error processing camera at index {i}");
                    }
                }
            }
            catch (DllNotFoundException)
            {
                logger.Warn("libASICamera2 not found - ZWO SDK may not be installed or library path is incorrect");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error detecting ZWO cameras");
            }

            return cameras;
        }

        public async Task<List<ICameraControl>> GetControls(Camera camera)
        {
            Guard.IsNotNull(camera);
            
            List<ICameraControl> controls = [];

            try
            {
                // Parse camera ID from path
                if (!int.TryParse(camera.Path, out int cameraId))
                {
                    logger.Warn($"Invalid camera ID in path: {camera.Path}");
                    return controls;
                }

                // Open camera temporarily to get controls
                int openResult = ZWOASICameraInterop.ASIOpenCamera(cameraId);
                if (openResult != (int)ZWOASICameraInterop.ASI_ERROR_CODE.ASI_SUCCESS)
                {
                    logger.Warn($"Failed to open camera {cameraId} for control detection, error: {openResult}");
                    // Return default controls even if we can't open
                    return GetDefaultControls(camera);
                }

                int initResult = ZWOASICameraInterop.ASIInitCamera(cameraId);
                if (initResult != (int)ZWOASICameraInterop.ASI_ERROR_CODE.ASI_SUCCESS)
                {
                    logger.Warn($"Failed to init camera {cameraId} for control detection, error: {initResult}");
                    try
                    {
                        ZWOASICameraInterop.ASICloseCamera(cameraId);
                    }
                    catch (Exception ex)
                    {
                        logger.Debug(ex, $"Error closing camera {cameraId} after init failure");
                    }
                    return GetDefaultControls(camera);
                }

                try
                {
                    int numControls = 0;
                    int getNumResult = ZWOASICameraInterop.ASIGetNumOfControls(cameraId, ref numControls);

                    if (getNumResult == (int)ZWOASICameraInterop.ASI_ERROR_CODE.ASI_SUCCESS && numControls > 0)
                    {
                        logger.Debug($"Camera {cameraId} has {numControls} controls");

                        for (int i = 0; i < numControls; i++)
                        {
                            try
                            {
                                var controlCaps = new ZWOASICameraInterop.ASI_CONTROL_CAPS();
                                int capResult = ZWOASICameraInterop.ASIGetControlCaps(cameraId, i, ref controlCaps);

                                if (capResult == (int)ZWOASICameraInterop.ASI_ERROR_CODE.ASI_SUCCESS)
                                {
                                    string controlName = ByteArrayToString(controlCaps.Name);
                                    var controlType = (ZWOASICameraInterop.ASI_CONTROL_TYPE)controlCaps.ControlType;

                                    logger.Debug($"Detected control from SDK: '{controlName}' type={controlType} ({controlCaps.ControlType}) min={controlCaps.MinValue} max={controlCaps.MaxValue}");

                                    // Only expose Gain and Exposure controls
                                    if (controlType == ZWOASICameraInterop.ASI_CONTROL_TYPE.ASI_GAIN ||
                                        controlType == ZWOASICameraInterop.ASI_CONTROL_TYPE.ASI_EXPOSURE)
                                    {
                                        ControlType mappedControlType = controlType == ZWOASICameraInterop.ASI_CONTROL_TYPE.ASI_GAIN
                                            ? ControlType.Gain
                                            : ControlType.ExposureTime;

                                        var cameraControl = new CameraControl(mappedControlType, camera)
                                        {
                                            Min = (int)controlCaps.MinValue,
                                            Max = (int)controlCaps.MaxValue,
                                            Step = 1,
                                            Default = (int)controlCaps.DefaultValue,
                                            Value = (int)controlCaps.DefaultValue,
                                            AutoSupported = controlCaps.IsAutoSupported != 0,
                                            Flags = controlCaps.IsWritable != 0 ? "rw" : "ro",
                                            ValueType = ControlValueType.Int
                                        };

                                        long currentValue = cameraControl.Value;
                                        int autoFlag = 0;
                                        int valueResult = ZWOASICameraInterop.ASIGetControlValue(cameraId, controlCaps.ControlType, ref currentValue, ref autoFlag);
                                        if (valueResult == (int)ZWOASICameraInterop.ASI_ERROR_CODE.ASI_SUCCESS)
                                        {
                                            cameraControl.Value = (int)currentValue;
                                            cameraControl.IsAuto = autoFlag != 0;
                                        }

                                        // Default policy:
                                        // - Exposure should start in auto mode.
                                        // - Gain should start in manual mode.
                                        if (cameraControl.AutoSupported)
                                        {
                                            if (mappedControlType == ControlType.ExposureTime && !cameraControl.IsAuto)
                                            {
                                                int setAutoResult = ZWOASICameraInterop.ASISetControlValue(
                                                    cameraId,
                                                    controlCaps.ControlType,
                                                    cameraControl.Value,
                                                    1);

                                                if (setAutoResult == (int)ZWOASICameraInterop.ASI_ERROR_CODE.ASI_SUCCESS)
                                                {
                                                    cameraControl.IsAuto = true;
                                                }
                                            }
                                            else if (mappedControlType == ControlType.Gain && cameraControl.IsAuto)
                                            {
                                                int setManualResult = ZWOASICameraInterop.ASISetControlValue(
                                                    cameraId,
                                                    controlCaps.ControlType,
                                                    cameraControl.Value,
                                                    0);

                                                if (setManualResult == (int)ZWOASICameraInterop.ASI_ERROR_CODE.ASI_SUCCESS)
                                                {
                                                    cameraControl.IsAuto = false;
                                                }
                                            }
                                        }

                                        controls.Add(cameraControl);
                                        logger.Debug($"Added control: {controlName} (min: {controlCaps.MinValue}, max: {controlCaps.MaxValue})");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                logger.Debug(ex, $"Error getting control caps for control index {i}");
                            }
                        }
                    }
                    else
                    {
                        logger.Warn($"Failed to get control count for camera {cameraId}");
                    }
                }
                finally
                {
                    // Always close camera after reading controls
                    try
                    {
                        ZWOASICameraInterop.ASICloseCamera(cameraId);
                    }
                    catch (Exception ex)
                    {
                        logger.Debug(ex, $"Error closing camera {cameraId}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error getting controls for camera '{camera.Name}'");
            }

            // If we couldn't get real controls, return defaults
            if (controls.Count == 0)
            {
                controls = GetDefaultControls(camera);
            }
            else
            {
                // Some camera/driver combinations report only one control in video mode.
                // Ensure both expected controls exist in the UI.
                bool hasGain = controls.Any(c => c.Name == ControlType.Gain);
                bool hasExposure = controls.Any(c => c.Name == ControlType.ExposureTime);

                if (!hasGain)
                {
                    controls.Add(new CameraControl(ControlType.Gain, camera)
                    {
                        Min = 0,
                        Max = 600,
                        Step = 1,
                        Default = 100,
                        Value = 100,
                        AutoSupported = true,
                        IsAuto = false,
                        Flags = "rw",
                        ValueType = ControlValueType.Int
                    });
                }

                if (!hasExposure)
                {
                    controls.Add(new CameraControl(ControlType.ExposureTime, camera)
                    {
                        Min = 1,
                        Max = 10000,
                        Step = 1,
                        Default = 100,
                        Value = 100,
                        AutoSupported = true,
                        IsAuto = true,
                        Flags = "rw",
                        ValueType = ControlValueType.Int
                    });
                }
            }

            return await Task.FromResult(controls);
        }

        private List<ICameraControl> GetDefaultControls(Camera camera)
        {
            List<ICameraControl> controls = [];

            // Add default Gain control (typical range: 0-600)
            var gainControl = new CameraControl(ControlType.Gain, camera)
            {
                Min = 0,
                Max = 600,
                Step = 1,
                Default = 100,
                Value = 100,
                AutoSupported = true,
                IsAuto = false,
                Flags = "rw",
                ValueType = ControlValueType.Int
            };
            controls.Add(gainControl);

            // Add default Exposure control (typical range: 1-10000 ms)
            var exposureControl = new CameraControl(ControlType.ExposureTime, camera)
            {
                Min = 1,
                Max = 10000,
                Step = 1,
                Default = 100,
                Value = 100,
                AutoSupported = true,
                IsAuto = true,
                Flags = "rw",
                ValueType = ControlValueType.Int
            };
            controls.Add(exposureControl);

            return controls;
        }

        public List<string> GetCommandLineParameters(Camera camera, ICommandBuilder? builder)
        {
            Guard.IsNotNull(camera);
            return [];
        }

        public void SetControl(Camera camera, ControlType controlType, double value)
        {
            Guard.IsNotNull(camera);

            try
            {
                if (!int.TryParse(camera.Path, out int cameraId))
                {
                    logger.Warn($"Invalid camera ID: {camera.Path}");
                    return;
                }

                // Get control type code
                int controlTypeCode = controlType switch
                {
                    ControlType.Gain => (int)ZWOASICameraInterop.ASI_CONTROL_TYPE.ASI_GAIN,
                    ControlType.ExposureTime => (int)ZWOASICameraInterop.ASI_CONTROL_TYPE.ASI_EXPOSURE,
                    _ => -1
                };

                if (controlTypeCode < 0)
                {
                    logger.Debug($"Control type {controlType} not supported for ZWO camera");
                    return;
                }

                // If the camera is already open for live streaming, skip the open/close
                // cycle – just set the value directly (the SDK handle is already valid).
                bool streaming = ZWOLiveStreamService.IsStreaming(cameraId);

                if (!streaming)
                {
                    int openResult = ZWOASICameraInterop.ASIOpenCamera(cameraId);
                    if (openResult != (int)ZWOASICameraInterop.ASI_ERROR_CODE.ASI_SUCCESS)
                    {
                        logger.Warn($"Failed to open camera {cameraId} for setting control");
                        return;
                    }

                    int initResult = ZWOASICameraInterop.ASIInitCamera(cameraId);
                    if (initResult != (int)ZWOASICameraInterop.ASI_ERROR_CODE.ASI_SUCCESS)
                    {
                        logger.Warn($"Failed to init camera {cameraId} for setting control, error: {initResult}");
                        ZWOASICameraInterop.ASICloseCamera(cameraId);
                        return;
                    }
                }

                try
                {
                    long longValue = (long)value;
                    int setResult = ZWOASICameraInterop.ASISetControlValue(cameraId, controlTypeCode, longValue, 0);

                    if (setResult == (int)ZWOASICameraInterop.ASI_ERROR_CODE.ASI_SUCCESS)
                    {
                        logger.Info($"Set {controlType} = {value} on camera {cameraId}");
                    }
                    else
                    {
                        logger.Warn($"Failed to set {controlType} on camera {cameraId}, error: {setResult}");
                    }
                }
                finally
                {
                    if (!streaming)
                    {
                        try
                        {
                            ZWOASICameraInterop.ASICloseCamera(cameraId);
                        }
                        catch (Exception ex)
                        {
                            logger.Debug(ex, $"Error closing camera {cameraId}");
                        }
                    }
                }
            }
            catch (DllNotFoundException)
            {
                logger.Warn("libASICamera2 not found");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error setting {controlType} on camera");
            }
        }

        public void SetControlAuto(Camera camera, ControlType controlType, bool isAuto)
        {
            Guard.IsNotNull(camera);

            try
            {
                if (!int.TryParse(camera.Path, out int cameraId))
                {
                    logger.Warn($"Invalid camera ID: {camera.Path}");
                    return;
                }

                int controlTypeCode = controlType switch
                {
                    ControlType.Gain => (int)ZWOASICameraInterop.ASI_CONTROL_TYPE.ASI_GAIN,
                    ControlType.ExposureTime => (int)ZWOASICameraInterop.ASI_CONTROL_TYPE.ASI_EXPOSURE,
                    _ => -1
                };

                if (controlTypeCode < 0)
                {
                    return;
                }

                bool streaming = ZWOLiveStreamService.IsStreaming(cameraId);

                if (!streaming)
                {
                    int openResult = ZWOASICameraInterop.ASIOpenCamera(cameraId);
                    if (openResult != (int)ZWOASICameraInterop.ASI_ERROR_CODE.ASI_SUCCESS)
                    {
                        logger.Warn($"Failed to open camera {cameraId} for setting auto mode");
                        return;
                    }

                    int initResult = ZWOASICameraInterop.ASIInitCamera(cameraId);
                    if (initResult != (int)ZWOASICameraInterop.ASI_ERROR_CODE.ASI_SUCCESS)
                    {
                        logger.Warn($"Failed to init camera {cameraId} for setting auto mode, error: {initResult}");
                        ZWOASICameraInterop.ASICloseCamera(cameraId);
                        return;
                    }
                }

                try
                {
                    long currentValue = 0;
                    int currentAuto = 0;
                    int getResult = ZWOASICameraInterop.ASIGetControlValue(cameraId, controlTypeCode, ref currentValue, ref currentAuto);

                    if (getResult != (int)ZWOASICameraInterop.ASI_ERROR_CODE.ASI_SUCCESS)
                    {
                        logger.Warn($"Failed to read current value for {controlType} on camera {cameraId}, error: {getResult}");
                        return;
                    }

                    int setResult = ZWOASICameraInterop.ASISetControlValue(cameraId, controlTypeCode, currentValue, isAuto ? 1 : 0);
                    if (setResult == (int)ZWOASICameraInterop.ASI_ERROR_CODE.ASI_SUCCESS)
                    {
                        logger.Info($"Set {controlType} auto={isAuto} on camera {cameraId}");
                    }
                    else
                    {
                        logger.Warn($"Failed to set {controlType} auto={isAuto} on camera {cameraId}, error: {setResult}");
                    }
                }
                finally
                {
                    if (!streaming)
                    {
                        try
                        {
                            ZWOASICameraInterop.ASICloseCamera(cameraId);
                        }
                        catch (Exception ex)
                        {
                            logger.Debug(ex, $"Error closing camera {cameraId}");
                        }
                    }
                }
            }
            catch (DllNotFoundException)
            {
                logger.Warn("libASICamera2 not found");
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error setting auto mode for {controlType}");
            }
        }

        private static string ByteArrayToString(byte[]? data)
        {
            if (data == null || data.Length == 0)
                return string.Empty;

            int nullIndex = System.Array.IndexOf(data, (byte)0);
            int length = nullIndex >= 0 ? nullIndex : data.Length;
            return Encoding.UTF8.GetString(data, 0, length);
        }
    }
}
