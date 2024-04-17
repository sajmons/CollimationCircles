using Avalonia.Controls;
using CollimationCircles.Helper;
using CollimationCircles.Models;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
        public void Set(string propertyname, double value, Camera camera)
        {

            if (camera.APIType is APIType.V4l2 || camera.APIType is APIType.QTCapture)
            {
                AppService.ExecuteCommand("v4l2-ctl", [
                    "--device", camera.Path,
                    $"--set-ctrl={v4l2controls[propertyname]}={value}"
                ]);                
            }
            else if (camera.APIType is APIType.Dshow)
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
            else if (camera.APIType is APIType.LibCamera)
            {
                // TODO: implement libcamera camera controls set
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

        public static async Task<List<CameraControl>> GetV4L2CameraControls(Camera camera)
        {
            var (errorCode, result, process) = await AppService.ExecuteCommand(
                "v4l2-ctl",
                ["--list-ctrls", "--device", $"{camera.Path}"]);

            string pattern = @"(?<name>\w+)\s(?<hex>0x\w+)\s\((?<type>\w+)\)\s*:\s*(min=(?<min>-?\d+))?\s*(max=(?<max>-?\d+))?\s*(step=(?<step>-?\d+))?\s*(default=(?<default>-?\d+))?\s*(value=(?<value>-?\d+))?\s*(flags=(?<flags>\w+))?";

            List<CameraControl> controls = [];

            var matches = Regex.Matches(result, pattern);

            logger.Info($"Parsed {matches.Count} controls for '{camera.Name}'");

            foreach (Match m in matches.Cast<Match>())
            {
                _ = int.TryParse(m.Groups["min"].Value, out int min);
                _ = int.TryParse(m.Groups["max"].Value, out int max);
                _ = int.TryParse(m.Groups["step"].Value, out int step);
                _ = int.TryParse(m.Groups["default"].Value, out int deflt);
                _ = int.TryParse(m.Groups["value"].Value, out int value);

                var cameraControl = new CameraControl()
                {
                    Name = m.Groups["name"].Value,
                    Min = min,
                    Max = max,
                    Step = step,
                    Default = deflt,
                    Value = value,
                    Flags = m.Groups["flags"].Value
                };

                controls.Add(cameraControl);
                logger.Info($"Control '{cameraControl.Name} min: {cameraControl.Min} max: {cameraControl.Max} step: {cameraControl.Step} default: {cameraControl.Default} value: {cameraControl.Value}' for '{camera.Name}' added");
            }

            return controls;
        }

        public static List<Camera> GetCameraList()
        {
            List<Camera> cameras = [];

            if (OperatingSystem.IsWindows())
            {
                cameras.AddRange(ListWindowsWebCam.Load());
            }
            else
            {
                var raspiCameras = GetLibCameraCameras().GetAwaiter().GetResult();
                cameras.AddRange(raspiCameras);

                var v4l2Cameras = GetV4L2Cameras().GetAwaiter().GetResult();
                cameras.AddRange(v4l2Cameras);
            }

            cameras.Add(new Camera() { APIType = APIType.Remote });

            return cameras;
        }

        public static async Task<List<Camera>> GetLibCameraCameras()
        {
            List<Camera> cameras = [];

            var (errorCode, result, process) = await AppService.ExecuteCommand(
                "libcamera-vid",
                ["--list-cameras"]);

            if (errorCode == 0)
            {
                string pattern = @"^(?<index>\d+)\s:\s(?<name>\w+)\s\[?.+\]\s\((?<path>.+)\)$";

                var matches = Regex.Matches(result, pattern, RegexOptions.Multiline);

                if (matches.Count > 0)
                {
                    logger.Info($"Parsed {matches.Count} LibCamera cameras");

                    foreach (Match m in matches.Cast<Match>())
                    {
                        _ = int.TryParse(m.Groups["index"].Value, out int index);

                        var camera = new Camera()
                        {
                            Index = index,
                            APIType = APIType.LibCamera,
                            Name = m.Groups["name"].Value,
                            Path = m.Groups["path"].Value
                        };

                        cameras.Add(camera);

                        logger.Info($"Adding camera: '{camera.Path}'");
                    }
                }
                else
                {
                    logger.Info($"No LibCamera camera parsed!");
                }
            }
            else
            {
                logger.Info($"No LibCamera compatible cameras detected!");
            }

            return cameras;
        }

        public static async Task<List<Camera>> GetV4L2Cameras()
        {
            List<Camera> cameras = [];

            var (errorCode, result, process) = await AppService.ExecuteCommand(
                "v4l2-ctl",
                ["--list-devices"]);

            logger.Info($"v4l2-ctl --list-devices result: {result}");

            if (errorCode == 0)
            {
                string pattern = @"^(.*usb.*):\n((\s*\/dev\/.*\n)*).*$";

                var match = Regex.Match(result, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    string[] camStr = match.Groups[2].Value.Trim().Split("\t");

                    logger.Info($"Parsed {camStr.Length} V4L2 cameras");

                    for (int i = 0; i < camStr.Length; i++)
                    {
                        string cam = camStr[i].Trim();

                        Camera c = new()
                        {
                            Index = i,
                            APIType = APIType.V4l2,
                            Name = cam,
                            Path = cam                            
                        };

                        c.Controls = await GetV4L2CameraControls(c);

                        cameras.Add(c);

                        logger.Info($"Adding camera: '{camStr[i]}'");
                    }
                }
                else
                {
                    logger.Info($"No V4L2 camera parsed!");
                }
            }
            else
            {
                logger.Warn($"No V4L2 compatible cameras detected!");
            }

            return cameras;
        }
    }
}
