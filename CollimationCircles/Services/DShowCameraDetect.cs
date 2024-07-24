using CollimationCircles.Models;
using CommunityToolkit.Mvvm.DependencyInjection;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace CollimationCircles.Services
{
    internal class DShowCameraDetect(VideoCapture? videoCapture = null) : ICameraDetect
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly VideoCapture? videoCapture = videoCapture;

        public Dictionary<ControlType, object> ControlMapping => new()
        {
            { ControlType.Brightness, VideoCaptureProperties.Brightness },
            { ControlType.Contrast, VideoCaptureProperties.Contrast },
            { ControlType.Saturation, VideoCaptureProperties.Saturation },
            { ControlType.Hue, VideoCaptureProperties.Hue },
            { ControlType.Gamma, VideoCaptureProperties.Gamma },
            { ControlType.Gain, VideoCaptureProperties.Gain },
            { ControlType.AutoFocus, VideoCaptureProperties.AutoFocus },
            { ControlType.Focus, VideoCaptureProperties.Focus },
            //{ ControlType.AutoWhiteBalance, VideoCaptureProperties.AutoWB },
            { ControlType.Temperature, VideoCaptureProperties.Temperature },
            { ControlType.Sharpness, VideoCaptureProperties.Sharpness },
            //{ ControlType.AutoExposure, VideoCaptureProperties.AutoExposure },
            { ControlType.ExposureTime, VideoCaptureProperties.Exposure },
            { ControlType.Zoom_Absolute, VideoCaptureProperties.Zoom }
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
                                APIType = APIType.Dshow
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

            foreach (var prop in Enum.GetValues<VideoCaptureProperties>())
            {
                //double propVal = videoCapture.Get(prop);

                //if (propVal != -1)
                {
                    if (Enum.TryParse(prop.ToString(), out ControlType controlName))
                    {
                        var cameraControl = new CameraControl(controlName);

                        controls.Add(cameraControl);
                        logger.Info($"Control '{cameraControl.Name} min: {cameraControl.Min} max: {cameraControl.Max} step: {cameraControl.Step} default: {cameraControl.Default} value: {cameraControl.Value}' type: {cameraControl.ValueType} for '{camera.Name}' added");
                    }
                }
            }

            return controls;
        }

        public void SetControl(ICamera camera, ControlType controlName, double value)
        {
            try
            {
                if (videoCapture != null)
                {
                    if (videoCapture.IsOpened())
                    {
                        if (Enum.TryParse(ControlMapping[controlName].ToString(), out VideoCaptureProperties control))
                        {
                            videoCapture.Set(control, value);
                            logger.Info($"{nameof(VideoCaptureProperties)} property '{controlName}' set to '{value}'");
                        }
                    }
                }
            }
            catch (Exception exc)
            {
                logger.Error(exc);
            }
        }

        public List<string> GetCommandLineParameters(ICamera camera)
        {
            return ["dshow-size=640x480", "dshow-fps=30", "dshow-adev=none", "live-caching=300"];
        }
    }
}
