using CollimationCircles.Models;
using CommunityToolkit.Diagnostics;
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
            { ControlType.Gamma, "Gamma" }
        };

        public List<Camera> GetCameras()
        {
            List<Camera> cameras = [];

            if (OperatingSystem.IsWindows())
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE (PNPClass = 'Image' OR PNPClass = 'Camera')"))
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

        public List<ICameraControl> GetControls(Camera camera)
        {
            Guard.IsNotNull(camera);

            List<ICameraControl> controls = [];

            CameraControl cameraControl;

            controls.Add(cameraControl = new CameraControl(ControlType.Contrast, camera)
            {
                Default = 50,
                Value = 50,
                Min = 0,
                Max = 2,
                Step = 0.1
            });
            logger.Info($"Control '{cameraControl.Name} min: {cameraControl.Min} max: {cameraControl.Max} step: {cameraControl.Step} default: {cameraControl.Default} value: {cameraControl.Value}' type: {cameraControl.ValueType} for '{camera.Name}' added");

            controls.Add(cameraControl = new CameraControl(ControlType.Brightness, camera)
            {
                Default = 50,
                Value = 50,
                Min = 0,
                Max = 2,
                Step = 0.1
            });
            logger.Info($"Control '{cameraControl.Name} min: {cameraControl.Min} max: {cameraControl.Max} step: {cameraControl.Step} default: {cameraControl.Default} value: {cameraControl.Value}' type: {cameraControl.ValueType} for '{camera.Name}' added");

            controls.Add(cameraControl = new CameraControl(ControlType.Hue, camera)
            {
                Default = 0,
                Value = 0,
                Min = 0,
                Max = 360,
                Step = 1
            });
            logger.Info($"Control '{cameraControl.Name} min: {cameraControl.Min} max: {cameraControl.Max} step: {cameraControl.Step} default: {cameraControl.Default} value: {cameraControl.Value}' type: {cameraControl.ValueType} for '{camera.Name}' added");

            controls.Add(cameraControl = new CameraControl(ControlType.Saturation, camera)
            {
                Default = 40,
                Value = 40,
                Min = 0,
                Max = 3,
                Step = 0.1
            });
            logger.Info($"Control '{cameraControl.Name} min: {cameraControl.Min} max: {cameraControl.Max} step: {cameraControl.Step} default: {cameraControl.Default} value: {cameraControl.Value}' type: {cameraControl.ValueType} for '{camera.Name}' added");

            controls.Add(cameraControl = new CameraControl(ControlType.Gamma, camera)
            {
                Default = 8,
                Value = 8,
                Min = 0,
                Max = 10,
                Step = 0.1
            });
            logger.Info($"Control '{cameraControl.Name} min: {cameraControl.Min} max: {cameraControl.Max} step: {cameraControl.Step} default: {cameraControl.Default} value: {cameraControl.Value}' type: {cameraControl.ValueType} for '{camera.Name}' added");

            return controls;
        }

        public void SetControl(Camera camera, ControlType controlName, double value)
        {
            Guard.IsNotNull(camera);            

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

        public List<string> GetCommandLineParameters(Camera camera)
        {
            Guard.IsNotNull(camera);

            return [
                $":dshow-vdev={camera.Name}"
                , ":dshow-size=1024x768"
                , ":dshow-fps=30"
                , ":dshow-adev=none"
                , ":live-caching=300"
                //, ":dshow-config"
            ];
        }
    }
}
