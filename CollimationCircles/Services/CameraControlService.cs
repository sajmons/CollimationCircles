using CollimationCircles.Models;
using CommunityToolkit.Mvvm.DependencyInjection;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    internal class CameraControlService : ICameraControlService, IDisposable
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly object vc = new();

        private readonly Dictionary<ControlType, string> v4l2controls = new()
        {
            { ControlType.Brightness, "brightness" },
            { ControlType.Contrast, "contrast" },
            { ControlType.Saturation, "saturation" },
            { ControlType.Hue, "hue" },
            { ControlType.Gamma, "gamma" },
            { ControlType.Gain, "gain" },
            //{ ControlType.AutoFocus, "focus_auto" },
            { ControlType.Focus, "focus_absolute" },
            //{ ControlType.AutoWhiteBalance, "white_balance_temperature_auto" },
            { ControlType.Temperature, "white_balance_temperature" },
            { ControlType.Sharpness, "sharpness" },
            //{ ControlType.AutoExposure, "exposure_auto" },
            { ControlType.ExposureTime, "exposure_absolute" },
            { ControlType.Zoom, "zoom_absolute" }
        };

        private readonly Dictionary<ControlType, VideoCaptureProperties> uvcControls = new()
        {
            { ControlType.Brightness, VideoCaptureProperties.Brightness },
            { ControlType.Contrast, VideoCaptureProperties.Contrast },
            { ControlType.Saturation, VideoCaptureProperties.Saturation },
            { ControlType.Hue, VideoCaptureProperties.Hue },
            { ControlType.Gamma, VideoCaptureProperties.Gamma },
            { ControlType.Gain, VideoCaptureProperties.Gain },
            //{ ControlType.AutoFocus, VideoCaptureProperties.AutoFocus },
            { ControlType.Focus, VideoCaptureProperties.Focus },
            //{ ControlType.AutoWhiteBalance, VideoCaptureProperties.AutoWB },
            { ControlType.Temperature, VideoCaptureProperties.Temperature },
            { ControlType.Sharpness, VideoCaptureProperties.Sharpness },
            //{ ControlType.AutoExposure, VideoCaptureProperties.AutoExposure },
            { ControlType.ExposureTime, VideoCaptureProperties.Exposure },
            { ControlType.Zoom, VideoCaptureProperties.Zoom }
        };

        private static readonly Dictionary<ControlType, string> rpiControls = new()
        {
            { ControlType.Brightness, "brightness" },
            { ControlType.Contrast, "contrast" },
            { ControlType.Saturation, "saturation" },
            { ControlType.Gain, "gain" },
            //{ ControlType.AutoFocus, "autofocus-mode" },  // default or manual
            { ControlType.Focus, "lens-position" },
            //{ ControlType.AutoWhiteBalance, "awb" },      // auto 2500K to 8000K, incandescent 2500K to 3000K, tungsten 3000K to 3500K, fluorescent 4000K to 4700K, indoor 3000K to 5000K, daylight 5500K to 6500K, cloudy 7000K to 8500K
            { ControlType.Sharpness, "sharpness" },
            { ControlType.ExposureTime, "shutter" },
            { ControlType.Zoom, "roi" }
        };

        public CameraControlService()
        {
            if (OperatingSystem.IsWindows())
            {
                vc = new VideoCapture();
            }
        }
        public void Set(ControlType controlName, double value, Camera camera)
        {
            // set camera control for V4L2
            if (camera.APIType is APIType.V4l2 || camera.APIType is APIType.QTCapture)
            {
                AppService.ExecuteCommand("v4l2-ctl", [
                    "--device",
                    camera.Path,
                    $"--set-ctrl={v4l2controls[controlName]}={value}"
                ]);
            }
            // set camera control for DirectShow
            else if (camera.APIType is APIType.Dshow)
            {
                try
                {
                    if (((VideoCapture)vc).IsOpened())
                    {
                        ((VideoCapture)vc).Set(uvcControls[controlName], value);
                        logger.Info($"OpenCvSharp.VideoCapture property '{controlName}' set to '{value}'");
                    }
                }
                catch (Exception exc)
                {
                    logger.Error(exc);
                }
            }
            // set camera control for Raspberry PI Camera
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
                ILibVLCService lib = Ioc.Default.GetRequiredService<ILibVLCService>();
                ((VideoCapture)vc)?.Open(lib.Camera.Index);

                if (vc is not null)
                {
                    lib.Camera.Controls = GetDShowCameraControls((VideoCapture)vc, lib.Camera);
                }
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
                controls.Add($"--{rpiControls[ControlType.Brightness]} {brightness}");

            if (contrast != null)
                controls.Add($"--{rpiControls[ControlType.Contrast]} {contrast}");

            if (saturation != null)
                controls.Add($"--{rpiControls[ControlType.Saturation]} {saturation}");

            if (gain != null)
                controls.Add($"--{rpiControls[ControlType.Gain]} {gain}");

            //var afMode = autofocusMode ? "auto" : "manual";
            //controls.Add($"--{rpiControls["Autofocus"]} {afMode}");

            if (focus != null)
                controls.Add($"--{rpiControls[ControlType.Focus]} {focus}");

            //string wb = "auto";

            //if (whiteBalance >= 2500 && whiteBalance <= 3000)
            //    wb = "incandescent";

            //if (whiteBalance >= 3000 && whiteBalance <= 3500)
            //    wb = "tungsten";

            //if (whiteBalance >= 4000 && whiteBalance <= 4700)
            //    wb = "fluorescent";

            //if (whiteBalance >= 3000 && whiteBalance <= 5000)
            //    wb = "indoor";

            //if (whiteBalance >= 5500 && whiteBalance <= 6500)
            //    wb = "daylight";

            //if (whiteBalance >= 7000 && whiteBalance <= 8500)
            //    wb = "cloudy";

            //controls.Add($"--{rpiControls[ControlType.AutoWhiteBalance]} {wb}");

            if (sharpness != null)
                controls.Add($"--{rpiControls[ControlType.Sharpness]} {sharpness}");

            if (exposureTime != null)
                controls.Add($"--{rpiControls[ControlType.ExposureTime]} {exposureTime}");

            decimal? roi = 1.0M / zoom;
            if (zoom != null)
                controls.Add($"--{rpiControls[ControlType.Zoom]} {roi},{roi},{roi},{roi}");

            return controls;
        }

        public async Task<List<CameraControl>> GetV4L2CameraControls(Camera camera)
        {
            var (errorCode, result, process) = await AppService.ExecuteCommand(
                "v4l2-ctl",
                ["--list-ctrls", "--device", $"{camera.Path}"]);

            string pattern = @"(?<name>\w+)\s(?<hex>0x\w+)\s\((?<type>\w+)\)\s*:\s*(min=(?<min>-?\d+))?\s*(max=(?<max>-?\d+))?\s*(step=(?<step>-?\d+))?\s*(default=(?<default>-?\d+))?\s*(value=(?<value>-?\d+))?\s*(flags=(?<flags>\w+))?";

            List<CameraControl> controls = [];

            var matches = Regex.Matches(result, pattern);

            logger.Info($"Parsed {matches.Count} controls for '{camera.Name} {camera.Path}'");

            foreach (Match m in matches.Cast<Match>())
            {
                _ = int.TryParse(m.Groups["min"].Value, out int min);
                _ = int.TryParse(m.Groups["max"].Value, out int max);
                _ = int.TryParse(m.Groups["step"].Value, out int step);
                _ = int.TryParse(m.Groups["default"].Value, out int deflt);
                _ = int.TryParse(m.Groups["value"].Value, out int value);

                if (Enum.TryParse(m.Groups["name"].Value, out ControlType controlName))
                {
                    var cameraControl = new CameraControl(controlName)
                    {
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
            }

            return controls;
        }

        public List<CameraControl> GetDShowCameraControls(VideoCapture capture, Camera camera)
        {
            List<CameraControl> controls = [];

            foreach (var prop in Enum.GetValues<VideoCaptureProperties>())
            {
                double propVal = capture.Get(prop);

                if (propVal != -1)
                {
                    if (Enum.TryParse(prop.ToString(), out ControlType controlName))
                    {
                        var cameraControl = new CameraControl(controlName)
                        {
                            Value = (int)propVal,
                        };

                        controls.Add(cameraControl);
                        logger.Info($"Control '{cameraControl.Name} min: {cameraControl.Min} max: {cameraControl.Max} step: {cameraControl.Step} default: {cameraControl.Default} value: {cameraControl.Value}' for '{camera.Name}' added");
                    }
                }
            }

            return controls;
        }

        public List<Camera> GetCameraList()
        {
            List<Camera> cameras = [];

            if (OperatingSystem.IsWindows())
            {
                var dshowCameras = GetDShowCameras();
                cameras.AddRange(dshowCameras);
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

        private List<Camera> GetDShowCameras()
        {
            List<Camera> cameras = [];

            if (OperatingSystem.IsWindows())
            {
                using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE PNPClass = 'Camera'"))
                {
                    var devices = searcher.Get().Cast<ManagementObject>().ToList();

                    foreach (var device in devices)
                    {
                        if (device != null)
                        {
                            string deviceName = (string)device.GetPropertyValue("Name");
                            string deviceId = (string)device.GetPropertyValue("DeviceID");
                            var c = new Camera()
                            {
                                Name = deviceName,
                                Path = deviceId,
                                APIType = APIType.Dshow
                            };

                            cameras.Add(c);
                            logger.Info($"Adding camera: '{c.Name} {c.Path}'");
                        }
                    }
                };
            }

            return cameras;
        }

        public async Task<List<Camera>> GetLibCameraCameras()
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

        public async Task<List<Camera>> GetV4L2Cameras()
        {
            List<Camera> cameras = [];

            var (errorCode, result, process) = await AppService.ExecuteCommand(
                "v4l2-ctl",
                ["--list-devices"]);

            logger.Info($"v4l2-ctl --list-devices result: {result}");

            if (errorCode == 0)
            {
                string pattern = @"(.*).*(.*usb.*):\n((\s*\/dev\/.*\n)*).*";

                var match = Regex.Match(result, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    string name = match.Groups[1].Value.Trim();

                    string[] camStr = match.Groups[3].Value.Trim().Split("\t");

                    logger.Info($"Parsed {camStr.Length} V4L2 cameras");

                    for (int i = 0; i < camStr.Length; i++)
                    {
                        string cam = camStr[i].Trim();

                        Camera c = new()
                        {
                            Index = i,
                            APIType = APIType.V4l2,
                            Name = name,
                            Path = cam
                        };

                        c.Controls = new List<CameraControl>(await GetV4L2CameraControls(c));

                        if (c.Controls.Count > 0)
                        {
                            cameras.Add(c);
                            logger.Info($"Adding camera: '{c.Name} {c.Path}'");
                        }
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
