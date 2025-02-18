using CollimationCircles.Models;
using CommunityToolkit.Diagnostics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace CollimationCircles.Services
{
    internal class V4L2CameraDetect() : ICameraDetect
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public Dictionary<ControlType, object> ControlMapping => new()
        {
            { ControlType.Brightness, "brightness" },
            { ControlType.Contrast, "contrast" },
            { ControlType.Saturation, "saturation" },
            { ControlType.Hue, "hue" },
            { ControlType.Gamma, "gamma" },
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

        public List<Camera> GetCameras()
        {
            List<Camera> cameras = [];

            var (errorCode, result) = AppService.ExecuteCommandAsync(
                "v4l2-ctl",
            ["--list-devices"]).GetAwaiter().GetResult();

            logger.Info($"v4l2-ctl --list-devices result: {result}");

            if (errorCode == 0)
            {
                string pattern = @"(.*):.*.*usb.*:\n((\s*\/dev\/.*\n)*)";

                var match = Regex.Match(result, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    string name = match.Groups[1].Value.Trim();

                    string[] camStr = match.Groups[2].Value.Trim().Split("\t");

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

                        c.Controls = GetControls(c);

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

        public List<ICameraControl> GetControls(Camera camera)
        {
            Guard.IsNotNull(camera);

            var (errorCode, result) = AppService.ExecuteCommandAsync(
                "v4l2-ctl",
                ["--list-ctrls", "--device", $"{camera.Path}"]).GetAwaiter().GetResult();

            string pattern = @"(?<name>\w+)\s(?<hex>0x\w+)\s\((?<type>\w+)\)\s*:\s*(min=(?<min>-?\d+))?\s*(max=(?<max>-?\d+))?\s*(step=(?<step>-?\d+))?\s*(default=(?<default>-?\d+))?\s*(value=(?<value>-?\d+))?\s*(flags=(?<flags>\w+))?";

            List<ICameraControl> controls = [];

            var matches = Regex.Matches(result, pattern);

            logger.Info($"Parsed {matches.Count} controls for '{camera.Name} {camera.Path}'");

            foreach (Match m in matches.Cast<Match>())
            {
                _ = int.TryParse(m.Groups["min"].Value, out int min);
                _ = int.TryParse(m.Groups["max"].Value, out int max);
                _ = int.TryParse(m.Groups["step"].Value, out int step);
                _ = int.TryParse(m.Groups["default"].Value, out int deflt);
                _ = int.TryParse(m.Groups["value"].Value, out int value);
                _ = Enum.TryParse(m.Groups["type"].Value, out ControlValueType controlvalueType);
                string name = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(m.Groups["name"].Value);

                if (Enum.TryParse(name, out ControlType controlName))
                {
                    var cameraControl = new CameraControl(controlName, camera)
                    {
                        Min = min,
                        Max = max,
                        Step = step,
                        Default = deflt,
                        Value = value,
                        Flags = m.Groups["flags"].Value,
                        ValueType = controlvalueType
                    };

                    controls.Add(cameraControl);
                    logger.Info($"Control '{cameraControl.Name} min: {cameraControl.Min} max: {cameraControl.Max} step: {cameraControl.Step} default: {cameraControl.Default} value: {cameraControl.Value}' type: {cameraControl.ValueType} for '{camera.Name}' added");
                }
            }

            return controls;
        }

        public void SetControl(Camera camera, ControlType controlType, double value)
        {
            Guard.IsNotNull(camera);

            AppService.ExecuteCommandAsync("v4l2-ctl", [
                "--device",
                camera.Path,
                $"--set-ctrl={ControlMapping[controlType]}={value}"
                ]).GetAwaiter().GetResult();
        }

        public List<string> GetCommandLineParameters(Camera camera, ICommandBuilder? builder)
        {
            Guard.IsNotNull(camera);

            return [
                "chroma=mjpg"
            ];
        }
    }
}
