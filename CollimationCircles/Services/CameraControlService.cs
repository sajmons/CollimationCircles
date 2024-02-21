using OpenCvSharp;
using System;
using System.Collections.Generic;

namespace CollimationCircles.Services
{
    internal class CameraControlService : ICameraControlService, IDisposable
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly object vc = new();

        private readonly Dictionary<string, string> v4l2Properties = new()
        {
            { "Brightness", "brightness" },
            { "Contrast", "contrast" },
            { "Saturation", "saturation" },
            { "Hue", "hue" },
            { "Gamma", "gamma" },
            { "Gain", "gain" },
            { "Autofocus", "focus_auto" },
            { "Focus", "focus_absolute" },
            { "AutoWhiteBalance", "white_balance_temperature_auto" },
            { "Temperature", "white_balance_temperature" },
            { "Sharpness", "sharpness" },
            { "AutoExposure", "exposure_auto" },
            { "ExposureTime", "exposure_absolute" },
            { "Zoom", "zoom_absolute" }
        };

        private readonly Dictionary<string, VideoCaptureProperties> OpenCVProperties = new()
        {
            { "Brightness", VideoCaptureProperties.Brightness },
            { "Contrast", VideoCaptureProperties.Contrast },
            { "Saturation", VideoCaptureProperties.Saturation },
            { "Hue", VideoCaptureProperties.Hue },
            { "Gamma", VideoCaptureProperties.Gamma },
            { "Gain", VideoCaptureProperties.Gain },
            { "Autofocus", VideoCaptureProperties.AutoFocus },
            { "Focus", VideoCaptureProperties.Focus },
            { "AutoWhiteBalance", VideoCaptureProperties.AutoWB },
            { "Temperature", VideoCaptureProperties.Temperature },
            { "Sharpness", VideoCaptureProperties.Sharpness },
            { "AutoExposure", VideoCaptureProperties.AutoExposure },
            { "ExposureTime", VideoCaptureProperties.Exposure },
            { "Zoom", VideoCaptureProperties.Zoom }
        };

        public event EventHandler? OnOpened;
        public event EventHandler? OnReleased;

        public CameraControlService()
        {
            if (OperatingSystem.IsWindows())
            {
                vc = new VideoCapture();                
            }
        }
        public void Set(string propertyname, double value)
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                AppService.ExecuteCommand("v4l2-ctl", [
                    $"--set-ctrl={v4l2Properties[propertyname]}={value}"
                ]);
                logger.Info($"v4l2-ctl property '{propertyname}' set to '{value}'");
            }

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    if (((VideoCapture)vc).IsOpened())
                    {
                        ((VideoCapture)vc).Set(OpenCVProperties[propertyname], value);
                        logger.Info($"OpenCvSharp.VideoCapture property '{propertyname}' set to '{value}'");
                    }
                }
                catch (Exception exc)
                {
                    logger.Error(exc);
                }
            }
        }
        public void Dispose()
        {
            if (OperatingSystem.IsWindows())
            {
                if (vc is not null)
                {
                    ((VideoCapture)vc).Release();
                    ((VideoCapture)vc).Dispose();
                }
            }
        }

        public void Open()
        {
            if (OperatingSystem.IsWindows())
            {
                ((VideoCapture)vc).Open(0);                
            }

            OnOpened?.Invoke(this, new EventArgs());
        }

        public void Release()
        {
            if (OperatingSystem.IsWindows())
            {
                ((VideoCapture)vc).Release();                
            }

            OnReleased?.Invoke(this, new EventArgs());
        }
    }
}
