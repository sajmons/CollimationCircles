using CollimationCircles.Models;
using CommunityToolkit.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CollimationCircles.Services
{
    internal class RasPiCameraDetect() : ICameraDetect
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public Dictionary<ControlType, object> ControlMapping => new()
        {
            { ControlType.Brightness, "brightness" },
            { ControlType.Contrast, "contrast" },
            { ControlType.Saturation, "saturation" },
            { ControlType.Gain, "gain" },
            //{ ControlType.AutoFocus, "autofocus-mode" },  // default or manual
            { ControlType.FocusAbsolute, "lens-position" },
            //{ ControlType.AutoWhiteBalance, "awb" },      // auto 2500K to 8000K, incandescent 2500K to 3000K, tungsten 3000K to 3500K, fluorescent 4000K to 4700K, indoor 3000K to 5000K, daylight 5500K to 6500K, cloudy 7000K to 8500K
            { ControlType.Sharpness, "sharpness" },
            { ControlType.ExposureTime, "shutter" },
            { ControlType.Zoom_Absolute, "roi" }
        };

        public List<Camera> GetCameras()
        {
            List<Camera> cameras = [];

            var (errorCode, result, process) = AppService.ExecuteCommand(
                "rpicam-vid",
                ["--list-cameras"]).GetAwaiter().GetResult();

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

        public List<ICameraControl> GetControls(Camera camera)
        {
            Guard.IsNotNull(camera);

            List<ICameraControl> controls = [];

            //decimal? brightness = null;
            //decimal? contrast = null;
            //decimal? saturation = null;
            //decimal? gain = null;
            //bool autofocusMode = true;
            //decimal? focus = null;
            //decimal? whiteBalance = null;
            //decimal? sharpness = null;
            //decimal? exposureTime = null;
            //decimal? zoom = null;

            //if (brightness != null)
            //    controls.Add($"--{ControlMapping[ControlType.Brightness]} {brightness}");

            //if (contrast != null)
            //    controls.Add($"--{ControlMapping[ControlType.Contrast]} {contrast}");

            //if (saturation != null)
            //    controls.Add($"--{ControlMapping[ControlType.Saturation]} {saturation}");

            //if (gain != null)
            //    controls.Add($"--{ControlMapping[ControlType.Gain]} {gain}");

            //var afMode = autofocusMode ? "auto" : "manual";
            //controls.Add($"--{rpiControls["Autofocus"]} {afMode}");

            //if (focus != null)
            //    controls.Add($"--{ControlMapping[ControlType.Focus]} {focus}");

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

            //if (sharpness != null)
            //    controls.Add($"--{ControlMapping[ControlType.Sharpness]} {sharpness}");

            //if (exposureTime != null)
            //    controls.Add($"--{ControlMapping[ControlType.ExposureTime]} {exposureTime}");

            //decimal? roi = 1.0M / zoom;
            //if (zoom != null)
            //    controls.Add($"--{ControlMapping[ControlType.Zoom_Absolute]} {roi},{roi},{roi},{roi}");

            return controls;
        }

        public void SetControl(Camera camera, ControlType controlName, double value)
        {
            Guard.IsNotNull(camera);

            // TODO: implement libcamera camera controls set
            // Stop video streaming and start new one with new parameters
            logger.Warn($"{nameof(RasPiCameraDetect)} is not implemented yet.");
        }

        public List<string> GetCommandLineParameters(Camera camera)
        {
            Guard.IsNotNull(camera);

            logger.Warn($"{nameof(RasPiCameraDetect)}: {nameof(GetCommandLineParameters)} not yet implemented");
            return [];
        }
    }
}
