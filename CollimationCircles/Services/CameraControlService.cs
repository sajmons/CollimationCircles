using OpenCvSharp;
using System;
using System.Collections.Generic;

namespace CollimationCircles.Services
{
    internal class CameraControlService : ICameraControlService, IDisposable
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly object vc = new();

        private readonly Dictionary<string, string> v4l2controls = new()
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

        private readonly Dictionary<string, VideoCaptureProperties> uvcControls = new()
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

        private static readonly Dictionary<string, string> rpiControls = new()
        {
            { "Brightness", "brightness" },
            { "Contrast", "contrast" },
            { "Saturation", "saturation" },
            { "Gain", "gain" },
            { "Autofocus", "autofocus-mode" },  // default or manual
            { "Focus", "lens-position" },
            { "AutoWhiteBalance", "awb" },      // auto 2500K to 8000K, incandescent 2500K to 3000K, tungsten 3000K to 3500K, fluorescent 4000K to 4700K, indoor 3000K to 5000K, daylight 5500K to 6500K, cloudy 7000K to 8500K
            { "Sharpness", "sharpness" },
            { "ExposureTime", "shutter" },
            { "Zoom", "roi" }
        };

        public CameraControlService()
        {
            if (OperatingSystem.IsWindows())
            {
                vc = new VideoCapture();
            }
        }
        public void Set(string propertyname, double value, StreamSource streamSource)
        {
            if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            {
                if (streamSource is StreamSource.UVC)
                {
                    AppService.ExecuteCommand("v4l2-ctl", [
                        $"--set-ctrl={v4l2controls[propertyname]}={value}"
                    ]);
                    logger.Info($"v4l2-ctl property '{propertyname}' set to '{value}'");
                }
            }

            if (OperatingSystem.IsWindows())
            {
                try
                {
                    if (((VideoCapture)vc).IsOpened())
                    {
                        ((VideoCapture)vc).Set(uvcControls[propertyname], value);
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
                ((VideoCapture)vc)?.Open(0);
            }
        }

        public void Release()
        {
            if (OperatingSystem.IsWindows())
            {
                ((VideoCapture)vc)?.Release();
            }
        }

        public static List<string> GetRaspberryPIControls(decimal? brightness = null, decimal? contrast = null, decimal? saturation = null,
            decimal? gain = null, bool autofocusMode = true, decimal? focus = null, decimal? whiteBalance = null, decimal? sharpness = null,
            decimal? exposureTime = null, decimal? zoom = null)
        {
            List<string> controls = new();

            if (brightness != null)
                controls.Add($"--{rpiControls["Brightness"]} {brightness}");

            if (contrast != null)
                controls.Add($"--{rpiControls["Contrast"]} {contrast}");

            if (saturation != null)
                controls.Add($"--{rpiControls["Saturation"]} {contrast}");

            if (gain != null)
                controls.Add($"--{rpiControls["Gain"]} {contrast}");

            var afMode = autofocusMode ? "auto" : "manual";
            controls.Add($"--{rpiControls["Autofocus"]} {afMode}");

            if (focus != null)
                controls.Add($"--{rpiControls["Focus"]} {focus}");

            string wb = "auto";

            if (whiteBalance >= 2500 && whiteBalance <= 3000)
                wb = "incandescent";

            if (whiteBalance >= 3000 && whiteBalance <= 3500)
                wb = "tungsten";

            if (whiteBalance >= 4000 && whiteBalance <= 4700)
                wb = "fluorescent";

            if (whiteBalance >= 3000 && whiteBalance <= 5000)
                wb = "indoor";

            if (whiteBalance >= 5500 && whiteBalance <= 6500)
                wb = "daylight";

            if (whiteBalance >= 7000 && whiteBalance <= 8500)
                wb = "cloudy";

            controls.Add($"--{rpiControls["AutoWhiteBalance"]} {wb}");

            if (sharpness != null)
                controls.Add($"--{rpiControls["Sharpness"]} {sharpness}");

            if (exposureTime != null)
                controls.Add($"--{rpiControls["ExposureTime"]} {exposureTime}");

            decimal? roi = 1.0M / zoom;
            if (zoom != null)
                controls.Add($"--{rpiControls["Zoom"]} {roi},{roi},{roi},{roi}");

            return controls;
        }
    }
}
