using CollimationCircles.Services;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CollimationCircles.Helper
{
    namespace RpiCameraTools
    {
        /// <summary>
        /// Enum for the supported rpicam–apps commands.
        /// </summary>
        public enum RpicamAppCommand
        {
            Vid,
            Still,
            Raw,
            Jpeg
        }

        /// <summary>
        /// A builder class to generate command–line strings for Raspberry Pi camera apps.
        /// The user sets the command type (Vid, Still, Raw, Jpeg) and then adds parameters.
        /// The builder will prevent use of options unsupported by the chosen command.
        /// </summary>
        public class RpiCameraAppsCommandBuilder : ICommandBuilder
        {
            private readonly Dictionary<string, string> _parameters;

            /// <summary>
            /// Gets or sets the command type.
            /// </summary>
            public RpicamAppCommand CommandType { get; set; } = RpicamAppCommand.Vid;

            /// <summary>
            /// Gets or sets the base command string. If not set explicitly,
            /// it will be derived from CommandType when building the command.
            /// </summary>
            public string? BaseCommand { get; set; }

            public RpiCameraAppsCommandBuilder()
            {
                _parameters = [];
            }

            /// <summary>
            /// Ensures that the current CommandType is among those allowed.
            /// Throws an exception if not.
            /// </summary>
            private void EnsureSupportedOption(string optionName, params RpicamAppCommand[] allowedFor)
            {
                if (!allowedFor.Contains(CommandType))
                {
                    throw new InvalidOperationException(
                        $"The option '{optionName}' is not supported for command '{CommandType}'. Allowed commands: {string.Join(", ", allowedFor)}.");
                }
            }

            /// <summary>
            /// Sets the capture duration in milliseconds (--timeout).
            /// Allowed for all commands.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetTimeout(int milliseconds)
            {
                _parameters.Add("--timeout", $"{milliseconds}");
                return this;
            }

            /// <summary>
            /// Adds the --inline flag.
            /// Supported only for rpicam-vid.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetInline(bool inline)
            {
                EnsureSupportedOption("--inline", RpicamAppCommand.Vid);
                if (inline)
                    _parameters.Add("--inline", "");
                return this;
            }

            /// <summary>
            /// Adds the --nopreview flag.
            /// Supported for Vid, Still, Jpeg (raw may not use preview).
            /// </summary>
            public RpiCameraAppsCommandBuilder SetNoPreview(bool nopreview)
            {
                EnsureSupportedOption("--nopreview", RpicamAppCommand.Vid, RpicamAppCommand.Still, RpicamAppCommand.Jpeg);
                if (nopreview)
                    _parameters.Add("--nopreview", "");
                return this;
            }

            /// <summary>
            /// Adds the --listen flag.
            /// Supported only for rpicam-vid.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetListen(bool listen)
            {
                EnsureSupportedOption("--listen", RpicamAppCommand.Vid);
                if (listen)
                    _parameters.Add("--listen", "");
                return this;
            }

            /// <summary>
            /// Adds the --listen flag.
            /// Supported only for rpicam-vid.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetFlush(bool flush)
            {
                EnsureSupportedOption("--flush", RpicamAppCommand.Vid);
                if (flush)
                    _parameters.Add("--flush", "");
                return this;
            }

            /// <summary>
            /// Sets the output target (--output).
            /// Allowed for all commands.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetOutput(string output)
            {
                if (!string.IsNullOrWhiteSpace(output))
                    _parameters.Add("--output", $"{output}");
                return this;
            }

            /// <summary>
            /// Sets the denoise option (--denoise).
            /// Supported for Vid, Still, and Jpeg.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetDenoise(string denoiseValue)
            {
                EnsureSupportedOption("--denoise", RpicamAppCommand.Vid, RpicamAppCommand.Still, RpicamAppCommand.Jpeg);
                if (!string.IsNullOrWhiteSpace(denoiseValue))
                    _parameters.Add("--denoise", $"{denoiseValue}");
                return this;
            }

            /// <summary>
            /// Sets the framerate (--framerate).
            /// Supported only for rpicam-vid.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetFramerate(int framerate)
            {
                EnsureSupportedOption("--framerate", RpicamAppCommand.Vid);
                _parameters.Add("--framerate", $"{framerate}");
                return this;
            }

            /// <summary>
            /// Sets the codec (--codec).
            /// Supported only for rpicam-vid.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetCodec(string codec)
            {
                EnsureSupportedOption("--codec", RpicamAppCommand.Vid);
                if (!string.IsNullOrWhiteSpace(codec))
                    _parameters.Add("--codec", "{codec}");
                return this;
            }

            /// <summary>
            /// Sets the level (--level).
            /// Supported only for rpicam-vid.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetLevel(double level)
            {
                EnsureSupportedOption("--level", RpicamAppCommand.Vid);
                _parameters.Add($"--level", $"{level.ToString(CultureInfo.InvariantCulture)}");
                return this;
            }

            /// <summary>
            /// Sets the viewfinder mode (--viewfinder-mode).
            /// Supported only for rpicam-vid.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetViewfinderMode(string mode)
            {
                EnsureSupportedOption("--viewfinder-mode", RpicamAppCommand.Vid);
                if (!string.IsNullOrWhiteSpace(mode))
                    _parameters.Add("--viewfinder-mode", $"{mode}");
                return this;
            }

            /// <summary>
            /// Sets the profile (--profile).
            /// Supported only for rpicam-vid.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetProfile(string profile)
            {
                EnsureSupportedOption("--profile", RpicamAppCommand.Vid);
                if (!string.IsNullOrWhiteSpace(profile))
                    _parameters.Add("--profile", $"{profile}");
                return this;
            }

            /// <summary>
            /// Sets the intra parameter (--intra).
            /// Supported only for rpicam-vid.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetIntra(int intra)
            {
                EnsureSupportedOption("--intra", RpicamAppCommand.Vid);
                _parameters.Add("--intra", $"{intra}");
                return this;
            }

            /// <summary>
            /// Sets the image width (--width).
            /// Allowed for Vid, Still, Raw, Jpeg.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetWidth(int width)
            {
                _parameters.Add("--width", $"{width}");
                return this;
            }

            /// <summary>
            /// Sets the image height (--height).
            /// Allowed for Vid, Still, Raw, Jpeg.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetHeight(int height)
            {
                _parameters.Add("--height", $"{height}");
                return this;
            }

            /// <summary>
            /// Sets the sharpness (--sharpness).
            /// Supported for Vid, Still, and Jpeg.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetSharpness(double sharpness)
            {
                EnsureSupportedOption("--sharpness", RpicamAppCommand.Vid, RpicamAppCommand.Still, RpicamAppCommand.Jpeg);
                _parameters.Add("--sharpness", $"{sharpness.ToString(CultureInfo.InvariantCulture)}");
                return this;
            }

            /// <summary>
            /// Sets the gain (--gain).
            /// Supported for Vid, Still, and Jpeg.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetGain(double gain)
            {
                EnsureSupportedOption("--gain", RpicamAppCommand.Vid, RpicamAppCommand.Still, RpicamAppCommand.Jpeg);
                _parameters.Add("--gain", $"{gain.ToString(CultureInfo.InvariantCulture)}");
                return this;
            }

            /// <summary>
            /// Sets the shutter time (--shutter).
            /// Supported for Vid, Still, and Jpeg.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetShutter(int shutter)
            {
                EnsureSupportedOption("--shutter", RpicamAppCommand.Vid, RpicamAppCommand.Still, RpicamAppCommand.Jpeg);
                _parameters.Add("--shutter", $"{shutter}");
                return this;
            }

            /// <summary>
            /// Sets the AWB gains (--awbgains).
            /// Supported for Vid, Still, and Jpeg.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetAwbGains(double red, double blue)
            {
                EnsureSupportedOption("--awbgains", RpicamAppCommand.Vid, RpicamAppCommand.Still, RpicamAppCommand.Jpeg);
                string gains = $"{red.ToString(CultureInfo.InvariantCulture)},{blue.ToString(CultureInfo.InvariantCulture)}";
                _parameters.Add("--awbgains", $"{gains}");
                return this;
            }

            /// <summary>
            /// Sets the metering mode (--metering).
            /// Supported for Vid, Still, and Jpeg.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetMetering(string metering)
            {
                EnsureSupportedOption("--metering", RpicamAppCommand.Vid, RpicamAppCommand.Still, RpicamAppCommand.Jpeg);
                if (!string.IsNullOrWhiteSpace(metering))
                    _parameters.Add("--metering", $"{metering}");
                return this;
            }

            /// <summary>
            /// Sets the brightness (--brightness).
            /// Supported for Vid, Still, and Jpeg.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetBrightness(double brightness)
            {
                EnsureSupportedOption("--brightness", RpicamAppCommand.Vid, RpicamAppCommand.Still, RpicamAppCommand.Jpeg);
                _parameters.Add("--brightness", $"{brightness.ToString(CultureInfo.InvariantCulture)}");
                return this;
            }

            /// <summary>
            /// Sets the contrast (--contrast).
            /// Supported for Vid, Still, and Jpeg.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetContrast(double contrast)
            {
                EnsureSupportedOption("--contrast", RpicamAppCommand.Vid, RpicamAppCommand.Still, RpicamAppCommand.Jpeg);
                _parameters.Add("--contrast", $"{contrast.ToString(CultureInfo.InvariantCulture)}");
                return this;
            }

            /// <summary>
            /// Sets the saturation (--saturation).
            /// Supported for Vid, Still, and Jpeg.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetSaturation(double saturation)
            {
                EnsureSupportedOption("--saturation", RpicamAppCommand.Vid, RpicamAppCommand.Still, RpicamAppCommand.Jpeg);
                _parameters.Add("--saturation", $"{saturation.ToString(CultureInfo.InvariantCulture)}");
                return this;
            }

            /// <summary>
            /// Sets a region of interest (--roi) using normalized coordinates (x, y, w, h).
            /// Allowed for all commands.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetROI(double x, double y, double w, double h)
            {
                string roiValue = string.Format(CultureInfo.InvariantCulture, "{0:F4},{1:F4},{2:F4},{3:F4}", x, y, w, h);
                _parameters.Add("--roi", $"{roiValue}");
                return this;
            }

            /// <summary>
            /// Helper method to automatically set ROI for a given digital zoom factor.
            /// For zoom factor Z, the ROI is ((1 - 1/Z)/2, (1 - 1/Z)/2, 1/Z, 1/Z).
            /// Allowed for all commands.
            /// </summary>
            public RpiCameraAppsCommandBuilder SetDigitalZoom(double zoomFactor)
            {
                if (zoomFactor < 1)
                    throw new ArgumentException("Zoom factor must be >= 1", nameof(zoomFactor));
                double fraction = 1.0 / zoomFactor;
                double offset = (1.0 - fraction) / 2.0;
                return SetROI(offset, offset, fraction, fraction);
            }

            /// <summary>
            /// Adds any custom parameter string.
            /// Allowed for all commands.
            /// </summary>
            public RpiCameraAppsCommandBuilder AddParameter(string parameter, string value)
            {
                if (!string.IsNullOrWhiteSpace(parameter))
                    _parameters.Add(parameter, value);
                return this;
            }

            /// <summary>
            /// Set default parameters
            /// </summary>
            public RpiCameraAppsCommandBuilder SetDefaultParameters()
            {
                _parameters.Clear();

                SetTimeout(0)
                .SetInline(true)
                .SetNoPreview(true)
                .SetListen(true)
                .SetOutput($"tcp://0.0.0.0:{RasPiCameraDetect.StreamPort}")
                .SetDenoise("off")
                .SetFramerate(30)
                .SetGain(16)
                .SetShutter(60000)
                .SetMetering("average")
                .SetBrightness(0.5)
                .SetContrast(1.7)
                .SetSaturation(1.0)
                .SetWidth(1280)
                .SetHeight(720)
                .SetDigitalZoom(3)
                .SetFlush(true);

                return this;
            }

            /// <summary>
            /// Get list of parameters
            /// </summary>
            public List<string> GetParameterList()
            {
                List<string> result = [];

                foreach (var parameter in _parameters)
                {
                    result.Add(parameter.Key);
                    result.Add(parameter.Value);
                }

                return result;
            }

            /// <summary>
            /// Builds and returns the full command–line string.
            /// If BaseCommand is not explicitly set, it is determined from CommandType.
            /// </summary>
            public string BuildCommandLine()
            {
                // If BaseCommand is not set, determine it based on CommandType.
                if (string.IsNullOrWhiteSpace(BaseCommand))
                {
                    BaseCommand = CommandType switch
                    {
                        RpicamAppCommand.Vid => "rpicam-vid",
                        RpicamAppCommand.Still => "rpicam-still",
                        RpicamAppCommand.Raw => "rpicam-raw",
                        RpicamAppCommand.Jpeg => "rpicam-jpeg",
                        _ => throw new InvalidOperationException("Unsupported command type."),
                    };
                }

                return $"{BaseCommand} {string.Join(" ", _parameters)}";
            }
        }
    }

}
