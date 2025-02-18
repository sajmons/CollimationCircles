using CollimationCircles.Helper.RpiCameraTools;
using CollimationCircles.Models;
using CommunityToolkit.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    internal class RasPiCameraDetect() : ICameraDetect
    {
        public const string StreamPort = "55555";
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        RpiCameraAppsCommandBuilder? commandBuilder;

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

        public async Task<List<Camera>> GetCameras()
        {
            List<Camera> cameras = [];

            var (errorCode, result) = await AppService.StartProcessAsync(
                "rpicam-vid",
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

                        camera.Controls = await GetControls(camera);

                        if (camera.Controls.Count > 0)
                        {
                            cameras.Add(camera);

                            logger.Info($"Adding camera: '{camera.Path}'");
                        }
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

        public Task<List<ICameraControl>> GetControls(Camera camera)
        {
            Guard.IsNotNull(camera);

            List<ICameraControl> controls = [
                new CameraControl(ControlType.Brightness, camera),
                new CameraControl(ControlType.Contrast, camera),
                new CameraControl(ControlType.Saturation, camera),
                new CameraControl(ControlType.Gain, camera),
                new CameraControl(ControlType.Zoom_Absolute, camera),
                ];

            return Task.FromResult(controls);
        }

        public void SetControl(Camera camera, ControlType controlName, double value)
        {
            Guard.IsNotNull(camera);

            // Set command type to Vid (video capture)
            commandBuilder = new RpiCameraAppsCommandBuilder
            {
                CommandType = RpicamAppCommand.Vid
            };

            switch (controlName)
            {
                case ControlType.Brightness:
                    commandBuilder.SetBrightness(value);
                    break;
                case ControlType.Saturation:
                    commandBuilder.SetSaturation(value);
                    break;
                case ControlType.Contrast:
                    commandBuilder.SetContrast(value);
                    break;
                case ControlType.Zoom_Absolute:
                    commandBuilder.SetDigitalZoom(value);
                    break;
                case ControlType.Gain:
                    commandBuilder.SetGain(value);
                    break;
                case ControlType.ExposureTime:
                    commandBuilder.SetShutter((int)value);
                    break;
            }

            commandBuilder
                .SetTimeout(0)
                .SetInline(true)
                .SetNoPreview(true)
                .SetListen(true)
                .SetOutput($"tcp://0.0.0.0:{StreamPort}")
                .SetDenoise("off")
                .SetFramerate(30)
                .SetMetering("average")
                .SetWidth(1280)
                .SetHeight(720)
                .SetFlush(true);

            //List<string> controls = new RasPiCameraDetect().GetCommandLineParameters(camera, commandBuilder);
            //Task.Run(async () => await AppService.StartRaspberryPIStream(StreamPort, controls));
        }

        public List<string> GetCommandLineParameters(Camera camera, ICommandBuilder? builder)
        {
            Guard.IsNotNull(camera);

            return builder?.GetParameterList() ?? [];
        }
    }
}
