using CollimationCircles.Models;
using CommunityToolkit.Mvvm.DependencyInjection;
using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace CollimationCircles.Services
{
    internal class DShowCameraDetect() : ICameraDetect
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public Dictionary<ControlType, object> ControlMapping => new()
        {
            { ControlType.Brightness, "Brightness" },
            { ControlType.Contrast, "Contrast" },
            { ControlType.Saturation, "Saturation" },
            { ControlType.Hue, "Hue" },
            { ControlType.Gamma, "Gamma" },
            { ControlType.Gain, "gain" },
            //{ ControlType.AutoFocus, "focus_auto" },
            { ControlType.FocusAbsolute, "focus_absolute" },
            //{ ControlType.AutoWhiteBalance, "white_balance_temperature_auto" },
            { ControlType.Temperature, "white_balance_temperature" },
            { ControlType.Sharpness, "sharpness" },
            //{ ControlType.AutoExposure, "exposure_auto" },
            { ControlType.ExposureTime, "exposure_absolute" },
            { ControlType.Zoom_Absolute, "zoom_absolute" }
        };

        public List<ICamera> GetCameras()
        {
            List<ICamera> cameras = [];

            if (OperatingSystem.IsWindows())
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE PNPClass = 'Camera'"))
                {
                    var devices = searcher.Get().Cast<ManagementObject>().ToList();

                    ILibVLCService lib = Ioc.Default.GetRequiredService<ILibVLCService>();

                    int camIndex = 0;
                    foreach (var device in devices)
                    {
                        if (device != null)
                        {
                            string deviceName = (string)device.GetPropertyValue("Name");
                            string deviceId = (string)device.GetPropertyValue("DeviceID");

                            Camera c = new()
                            {
                                Name = deviceName,
                                Path = deviceId,
                                APIType = APIType.Dshow,
                                Index = camIndex++
                            };

                            c.Controls = GetControls(c);

                            if (c.Controls.Count > 0)
                            {
                                cameras.Add(c);
                                logger.Info($"Adding camera: '{c.Name} {c.Path}'");
                            }
                        }
                    }
                };
            }

            return cameras;
        }

        public List<ICameraControl> GetControls(ICamera camera)
        {
            List<ICameraControl> controls = [];

            CameraControl cameraControl;

            controls.Add(cameraControl = new CameraControl(ControlType.Contrast)
            {
                Min = 0,
                Max = 2,
                Step = 0.1
            });
            logger.Info($"Control '{cameraControl.Name} min: {cameraControl.Min} max: {cameraControl.Max} step: {cameraControl.Step} default: {cameraControl.Default} value: {cameraControl.Value}' type: {cameraControl.ValueType} for '{camera.Name}' added");

            controls.Add(cameraControl = new CameraControl(ControlType.Brightness)
            {
                Min = 0,
                Max = 2,
                Step = 0.1
            });
            logger.Info($"Control '{cameraControl.Name} min: {cameraControl.Min} max: {cameraControl.Max} step: {cameraControl.Step} default: {cameraControl.Default} value: {cameraControl.Value}' type: {cameraControl.ValueType} for '{camera.Name}' added");

            controls.Add(cameraControl = new CameraControl(ControlType.Hue)
            {
                Min = 0,
                Max = 360,
                Step = 1
            });
            logger.Info($"Control '{cameraControl.Name} min: {cameraControl.Min} max: {cameraControl.Max} step: {cameraControl.Step} default: {cameraControl.Default} value: {cameraControl.Value}' type: {cameraControl.ValueType} for '{camera.Name}' added");

            controls.Add(cameraControl = new CameraControl(ControlType.Saturation)
            {
                Min = 0,
                Max = 3,
                Step = 0.1
            });
            logger.Info($"Control '{cameraControl.Name} min: {cameraControl.Min} max: {cameraControl.Max} step: {cameraControl.Step} default: {cameraControl.Default} value: {cameraControl.Value}' type: {cameraControl.ValueType} for '{camera.Name}' added");

            controls.Add(cameraControl = new CameraControl(ControlType.Gamma)
            {
                Min = 0,
                Max = 10,
                Step = 0.1
            });
            logger.Info($"Control '{cameraControl.Name} min: {cameraControl.Min} max: {cameraControl.Max} step: {cameraControl.Step} default: {cameraControl.Default} value: {cameraControl.Value}' type: {cameraControl.ValueType} for '{camera.Name}' added");

            return controls;
        }

        public void SetControl(ICamera camera, ControlType controlName, double value)
        {
            try
            {
                ILibVLCService vlc = Ioc.Default.GetRequiredService<ILibVLCService>();
                
                if (Enum.TryParse(ControlMapping[controlName].ToString(), out VideoAdjustOption control))
                {
                    ICameraControl? cameraControl = camera.Controls.FirstOrDefault(c => c.Name == controlName);

                    if (cameraControl is not null)
                    {
                        float val = (float)ConvertRange(cameraControl.Min, cameraControl.Max, value);

                        vlc.MediaPlayer.SetAdjustFloat(control, val);
                        logger.Info($"{nameof(VideoAdjustOption)} property '{controlName}' set to '{value}'");
                    }
                }                
            }
            catch (Exception exc)
            {
                logger.Error(exc);
            }
        }

        public static double ConvertRange(double newStart, double newEnd, double value)
        {
            double originalStart = 0, originalEnd = 100;

            double scale = (double)(newEnd - newStart) / (originalEnd - originalStart);
            return (newStart + ((value - originalStart) * scale));
        }

        public List<string> GetCommandLineParameters(ICamera camera)
        {
            return [$":dshow-vdev={camera.Name}", ":dshow-size=640x480", ":dshow-fps=30", ":dshow-adev=none", ":live-caching=300"];
        }
    }
}
