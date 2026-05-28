using CollimationCircles.Models;
using CommunityToolkit.Diagnostics;
using DirectX.Capture;
using DShowNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    internal class DShowCameraDetect(bool displayAdvancedDShowDialog = false) : ICameraDetect
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public Dictionary<ControlType, object> ControlMapping => new()
        {
            { ControlType.Brightness, "Brightness" },
            { ControlType.Contrast, "Contrast" },
            { ControlType.Saturation, "Saturation" },
            { ControlType.Hue, "Hue" },
            { ControlType.Gamma, "Gamma" }
        };

        public async Task<List<Camera>> GetCameras()
        {
            List<Camera> cameras = [];

            try
            {
                Filters filters = new Filters();
                int camIndex = 0;                

                foreach (Filter device in filters.VideoInputDevices)
                {
                    if (device != null)
                    {
                        string deviceName = device.Name;

                        logger.Debug($"DeviceName: '{deviceName}'");

                        // treat as camera - all devices in VideoInputDevices are video input devices
                        Camera c = new()
                        {
                            Name = deviceName,
                            Path = deviceName,
                            APIType = APIType.Dshow,
                            Index = camIndex++
                        };

                        c.Controls = await GetControls(c);

                        //if (c.Controls.Count > 0)
                        {
                            cameras.Add(c);
                            logger.Info($"Adding camera: '{c.Name}'");
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                logger.Error(exc, "Error while detecting cameras using DirectX.Capture Filters");
            }

            return cameras;
        }

        public Task<List<ICameraControl>> GetControls(Camera camera)
        {
            Guard.IsNotNull(camera);

            List<ICameraControl> controls = [];

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return Task.FromResult(controls);
            }

            try
            {
                IFilterGraph2 grph = null;
                IBaseFilter camFltr = null;
                ICaptureGraphBuilder2 bldr = null;
                Filters filters = new Filters();

                try
                {
                    // Find the filter for this camera
                    Filter selectedFilter = null;
                    foreach (Filter f in filters.VideoInputDevices)
                    {
                        if (f.Name == camera.Name)
                        {
                            selectedFilter = f;
                            break;
                        }
                    }

                    if (selectedFilter == null)
                    {
                        logger.Warn($"Camera '{camera.Name}' not found in video input devices");
                        return Task.FromResult(controls);
                    }

                    grph = (IFilterGraph2)Activator.CreateInstance(Type.GetTypeFromCLSID(Clsid.FilterGraph, true));
                    bldr = (ICaptureGraphBuilder2)Activator.CreateInstance(Type.GetTypeFromCLSID(Clsid.CaptureGraphBuilder2, true));
                    int hr = bldr.SetFiltergraph(grph as IGraphBuilder);
                    if (hr < 0)
                        Marshal.ThrowExceptionForHR(hr);

                    // Create and add the web-cam filter to the graph.
                    IMoniker moniker = selectedFilter.CreateMoniker();
                    if (moniker != null)
                    {
                        grph.AddSourceFilterForMoniker(moniker, null, selectedFilter.Name, out camFltr);
                        Marshal.ReleaseComObject(moniker);
                    }

                    if (camFltr == null)
                    {
                        logger.Warn($"Failed to create filter for camera '{camera.Name}'");
                        return Task.FromResult(controls);
                    }

                    // Try to get IAMVideoProcAmp interface
                    object comObj = null;
                    Guid iid = typeof(IAMVideoProcAmp).GUID;
                    Guid cat = PinCategory.Capture;
                    Guid type = DShowNET.MediaType.Video;

                    hr = bldr.FindInterface(ref cat, ref type, camFltr, ref iid, out comObj);

                    if (hr >= 0 && comObj is IAMVideoProcAmp videoProcAmp)
                    {
                        // Enumerate video processing properties
                        foreach (VideoProcAmpProperty prop in Enum.GetValues(typeof(VideoProcAmpProperty)))
                        {
                            int min, max, step, defaultValue, capFlags;
                            hr = videoProcAmp.GetRange(prop, out min, out max, out step, out defaultValue, out capFlags);

                            if (hr >= 0 && min != max)
                            {
                                // Map VideoProcAmpProperty to ControlType
                                if (TryMapVideoProcAmpToControlType(prop, out ControlType controlType))
                                {
                                    int currentValue = defaultValue;
                                    videoProcAmp.Get(prop, out currentValue, out int flags);

                                    var videoProcControl = new CameraControl(controlType, camera)
                                    {
                                        Min = min,
                                        Max = max,
                                        Step = step,
                                        Default = defaultValue,
                                        Value = currentValue,
                                        ValueType = ControlValueType.Int,
                                        Flags = ((int)capFlags).ToString()
                                    };

                                    controls.Add(videoProcControl);
                                    logger.Info($"Control '{controlType}' (VideoProcAmp) min: {min} max: {max} step: {step} default: {defaultValue} value: {currentValue} for '{camera.Name}' added");
                                }
                            }
                        }

                        if (comObj != null)
                            Marshal.ReleaseComObject(comObj);
                    }

                    // Try to get IAMCameraControl interface
                    comObj = null;
                    iid = typeof(IAMCameraControl).GUID;

                    hr = bldr.FindInterface(ref cat, ref type, camFltr, ref iid, out comObj);

                    if (hr >= 0 && comObj is IAMCameraControl camCtrl)
                    {
                        // Enumerate camera control properties
                        foreach (CameraControlProperty prop in Enum.GetValues(typeof(CameraControlProperty)))
                        {
                            int min, max, step, defaultValue;
                            CameraControlFlags capFlags;
                            hr = camCtrl.GetRange(prop, out min, out max, out step, out defaultValue, out capFlags);

                            if (hr >= 0 && min != max)
                            {
                                // Map CameraControlProperty to ControlType
                                if (TryMapCameraControlToControlType(prop, out ControlType controlType))
                                {
                                    int currentValue = defaultValue;
                                    CameraControlFlags flags;
                                    camCtrl.Get(prop, out currentValue, out flags);

                                    var camCtrlObj = new CameraControl(controlType, camera)
                                    {
                                        Min = min,
                                        Max = max,
                                        Step = step,
                                        Default = defaultValue,
                                        Value = currentValue,
                                        ValueType = ControlValueType.Int,
                                        Flags = flags.ToString()
                                    };

                                    // Check if this control is already added (avoid duplicates)
                                    if (!controls.Any(c => c.Name == controlType))
                                    {
                                        controls.Add(camCtrlObj);
                                        logger.Info($"Control '{controlType}' (CameraControl) min: {min} max: {max} step: {step} default: {defaultValue} value: {currentValue} for '{camera.Name}' added");
                                    }
                                }
                            }
                        }

                        if (comObj != null)
                            Marshal.ReleaseComObject(comObj);
                    }
                }
                finally
                {
                    if (grph != null)
                        Marshal.ReleaseComObject(grph);

                    if (camFltr != null)
                        Marshal.ReleaseComObject(camFltr);

                    if (bldr != null)
                        Marshal.ReleaseComObject(bldr);
                }
            }
            catch (Exception exc)
            {
                logger.Error(exc, $"Error while getting controls for camera '{camera.Name}'");
            }

            return Task.FromResult(controls);
        }

        private static bool TryMapVideoProcAmpToControlType(VideoProcAmpProperty prop, out ControlType controlType)
        {
            controlType = prop switch
            {
                VideoProcAmpProperty.Brightness => ControlType.Brightness,
                VideoProcAmpProperty.Contrast => ControlType.Contrast,
                VideoProcAmpProperty.Hue => ControlType.Hue,
                VideoProcAmpProperty.Saturation => ControlType.Saturation,
                VideoProcAmpProperty.Sharpness => ControlType.Sharpness,
                VideoProcAmpProperty.Gamma => ControlType.Gamma,
                VideoProcAmpProperty.WhiteBalance => ControlType.Temperature,
                VideoProcAmpProperty.BacklightCompensation => ControlType.ExposureTime,
                VideoProcAmpProperty.Gain => ControlType.Gain,
                _ => (ControlType)(-1)
            };

            return (int)controlType >= 0;
        }

        private static bool TryMapCameraControlToControlType(CameraControlProperty prop, out ControlType controlType)
        {
            controlType = prop switch
            {
                CameraControlProperty.Pan => (ControlType)(-1), // Not in ControlType enum
                CameraControlProperty.Tilt => (ControlType)(-1), // Not in ControlType enum
                CameraControlProperty.Roll => (ControlType)(-1), // Not in ControlType enum
                CameraControlProperty.Zoom => ControlType.Zoom_Absolute,
                CameraControlProperty.Exposure => ControlType.ExposureTime,
                CameraControlProperty.Iris => (ControlType)(-1), // Not in ControlType enum
                CameraControlProperty.Focus => ControlType.Focus,
                _ => (ControlType)(-1)
            };

            return (int)controlType >= 0;
        }

        private static bool TryMapControlTypeToVideoProcAmp(ControlType controlType, out VideoProcAmpProperty prop)
        {
            prop = controlType switch
            {
                ControlType.Brightness => VideoProcAmpProperty.Brightness,
                ControlType.Contrast => VideoProcAmpProperty.Contrast,
                ControlType.Hue => VideoProcAmpProperty.Hue,
                ControlType.Saturation => VideoProcAmpProperty.Saturation,
                ControlType.Sharpness => VideoProcAmpProperty.Sharpness,
                ControlType.Gamma => VideoProcAmpProperty.Gamma,
                ControlType.Temperature => VideoProcAmpProperty.WhiteBalance,
                ControlType.Gain => VideoProcAmpProperty.Gain,
                _ => (VideoProcAmpProperty)(-1)
            };

            return (int)prop >= 0;
        }

        private static bool TryMapControlTypeToCameraControl(ControlType controlType, out CameraControlProperty prop)
        {
            prop = controlType switch
            {
                ControlType.Zoom_Absolute => CameraControlProperty.Zoom,
                ControlType.Focus => CameraControlProperty.Focus,
                _ => (CameraControlProperty)(-1)
            };

            return (int)prop >= 0;
        }

        public void SetControl(Camera camera, ControlType controlName, double value)
        {
            Guard.IsNotNull(camera);

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return;
            }

            try
            {
                ICameraControl? cameraControl = camera.Controls.FirstOrDefault(c => c.Name == controlName);

                if (cameraControl is null)
                {
                    logger.Warn($"Control '{controlName}' not found for camera '{camera.Name}'");
                    return;
                }

                // Convert value from 0-100 range to the control's min-max range
                int convertedValue = (int)ConvertRange(cameraControl.Min, cameraControl.Max, value);

                IFilterGraph2 grph = null;
                IBaseFilter camFltr = null;
                ICaptureGraphBuilder2 bldr = null;
                Filters filters = new Filters();

                try
                {
                    // Find the filter for this camera
                    Filter selectedFilter = null;
                    foreach (Filter f in filters.VideoInputDevices)
                    {
                        if (f.Name == camera.Name)
                        {
                            selectedFilter = f;
                            break;
                        }
                    }

                    if (selectedFilter == null)
                    {
                        logger.Warn($"Camera '{camera.Name}' not found in video input devices");
                        return;
                    }

                    grph = (IFilterGraph2)Activator.CreateInstance(Type.GetTypeFromCLSID(Clsid.FilterGraph, true));
                    bldr = (ICaptureGraphBuilder2)Activator.CreateInstance(Type.GetTypeFromCLSID(Clsid.CaptureGraphBuilder2, true));
                    int hr = bldr.SetFiltergraph(grph as IGraphBuilder);
                    if (hr < 0)
                        Marshal.ThrowExceptionForHR(hr);

                    // Create and add the camera filter to the graph
                    IMoniker moniker = selectedFilter.CreateMoniker();
                    if (moniker != null)
                    {
                        grph.AddSourceFilterForMoniker(moniker, null, selectedFilter.Name, out camFltr);
                        Marshal.ReleaseComObject(moniker);
                    }

                    if (camFltr == null)
                    {
                        logger.Warn($"Failed to create filter for camera '{camera.Name}'");
                        return;
                    }

                    Guid cat = PinCategory.Capture;
                    Guid type = DShowNET.MediaType.Video;

                    // Try to set VideoProcAmp property
                    if (TryMapControlTypeToVideoProcAmp(controlName, out VideoProcAmpProperty videoProcAmpProp))
                    {
                        object comObj = null;
                        Guid iid = typeof(IAMVideoProcAmp).GUID;

                        hr = bldr.FindInterface(ref cat, ref type, camFltr, ref iid, out comObj);

                        if (hr >= 0 && comObj is IAMVideoProcAmp videoProcAmp)
                        {
                            hr = videoProcAmp.Set(videoProcAmpProp, convertedValue, 0);  // flags: 0 for no special flags
                            if (hr >= 0)
                            {
                                logger.Info($"VideoProcAmp property '{controlName}' set to '{convertedValue}' for camera '{camera.Name}'");
                            }
                            else
                            {
                                logger.Error($"Failed to set VideoProcAmp property '{controlName}' (HR: {hr:X8})");
                            }

                            if (comObj != null)
                                Marshal.ReleaseComObject(comObj);
                        }
                    }
                    // Try to set CameraControl property
                    else if (TryMapControlTypeToCameraControl(controlName, out CameraControlProperty camCtrlProp))
                    {
                        object comObj = null;
                        Guid iid = typeof(IAMCameraControl).GUID;

                        hr = bldr.FindInterface(ref cat, ref type, camFltr, ref iid, out comObj);

                        if (hr >= 0 && comObj is IAMCameraControl camCtrl)
                        {
                            hr = camCtrl.Set(camCtrlProp, convertedValue, CameraControlFlags.Manual);
                            if (hr >= 0)
                            {
                                logger.Info($"CameraControl property '{controlName}' set to '{convertedValue}' for camera '{camera.Name}'");
                            }
                            else
                            {
                                logger.Error($"Failed to set CameraControl property '{controlName}' (HR: {hr:X8})");
                            }

                            if (comObj != null)
                                Marshal.ReleaseComObject(comObj);
                        }
                    }
                    else
                    {
                        logger.Warn($"Control '{controlName}' cannot be mapped to a DirectShow property");
                    }
                }
                finally
                {
                    if (grph != null)
                        Marshal.ReleaseComObject(grph);

                    if (camFltr != null)
                        Marshal.ReleaseComObject(camFltr);

                    if (bldr != null)
                        Marshal.ReleaseComObject(bldr);
                }
            }
            catch (Exception exc)
            {
                logger.Error(exc, $"Error setting control '{controlName}' for camera '{camera.Name}'");
            }
        }

        public static double ConvertRange(double newStart, double newEnd, double value)
        {
            double originalStart = 0, originalEnd = 100;

            double scale = (double)(newEnd - newStart) / (originalEnd - originalStart);
            return (newStart + ((value - originalStart) * scale));
        }

        public List<string> GetCommandLineParameters(Camera camera, ICommandBuilder? builder)
        {
            Guard.IsNotNull(camera);

            List<string> properties = [
                $":dshow-vdev={camera.Name}"
                , ":dshow-adev=none"
                , ":live-caching=300"
                , ":dshow-size=640x480"
                , ":dshow-vfps=30"
                , ":avformat-format=rgb24"
            ];

            if (displayAdvancedDShowDialog)
            {
                properties.Add(":dshow-config");
            }

            return properties;
        }
    }
}
