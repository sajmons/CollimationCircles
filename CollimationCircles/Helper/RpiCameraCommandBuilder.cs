using System;
using System.Collections.Generic;
using System.Globalization;

namespace CollimationCircles.Helper
{
    /// <summary>
    /// A builder class to generate a command–line string for the rpicam-vid utility.
    /// </summary>
    public class RpiCameraCommandBuilder
    {
        private readonly List<string> _parameters;

        /// <summary>
        /// Gets or sets the base command (default: "rpicam-vid").
        /// </summary>
        public string BaseCommand { get; set; } = "rpicam-vid";

        public RpiCameraCommandBuilder()
        {
            _parameters = [];
        }

        /// <summary>
        /// Sets the capture duration in milliseconds using the -t flag.
        /// </summary>
        public RpiCameraCommandBuilder SetDuration(int milliseconds)
        {
            _parameters.Add($"-t {milliseconds}");
            return this;
        }

        /// <summary>
        /// Adds the --inline flag to force header information into each I-frame.
        /// </summary>
        public RpiCameraCommandBuilder SetInline(bool inline)
        {
            if (inline)
                _parameters.Add("--inline");
            return this;
        }

        /// <summary>
        /// Adds the --nopreview flag to disable the preview window.
        /// </summary>
        public RpiCameraCommandBuilder SetNoPreview(bool nopreview)
        {
            if (nopreview)
                _parameters.Add("--nopreview");
            return this;
        }

        /// <summary>
        /// Adds the --listen flag so the command waits for a TCP connection.
        /// </summary>
        public RpiCameraCommandBuilder SetListen(bool listen)
        {
            if (listen)
                _parameters.Add("--listen");
            return this;
        }

        /// <summary>
        /// Sets the output target (file or stream) with the -o flag.
        /// </summary>
        public RpiCameraCommandBuilder SetOutput(string output)
        {
            if (!string.IsNullOrWhiteSpace(output))
                _parameters.Add($"-o {output}");
            return this;
        }

        /// <summary>
        /// Sets the denoise option (for example, "off").
        /// </summary>
        public RpiCameraCommandBuilder SetDenoise(string denoiseValue)
        {
            if (!string.IsNullOrWhiteSpace(denoiseValue))
                _parameters.Add($"--denoise {denoiseValue}");
            return this;
        }

        /// <summary>
        /// Sets the framerate in frames per second.
        /// </summary>
        public RpiCameraCommandBuilder SetFramerate(int framerate)
        {
            _parameters.Add($"--framerate {framerate}");
            return this;
        }

        /// <summary>
        /// Sets the codec (for example, "h264" or "mjpeg").
        /// </summary>
        public RpiCameraCommandBuilder SetCodec(string codec)
        {
            if (!string.IsNullOrWhiteSpace(codec))
                _parameters.Add($"--codec {codec}");
            return this;
        }

        /// <summary>
        /// Sets the level parameter (for example, "4").
        /// </summary>
        public RpiCameraCommandBuilder SetLevel(int level)
        {
            _parameters.Add($"--level {level}");
            return this;
        }

        /// <summary>
        /// Sets the viewfinder mode. Typically this is in the format "width:height:bitdepth" (e.g. "1632:1224:10").
        /// </summary>
        public RpiCameraCommandBuilder SetViewfinderMode(string viewfinderMode)
        {
            if (!string.IsNullOrWhiteSpace(viewfinderMode))
                _parameters.Add($"--viewfinder-mode {viewfinderMode}");
            return this;
        }

        /// <summary>
        /// Sets the profile (for example, "baseline").
        /// </summary>
        public RpiCameraCommandBuilder SetProfile(string profile)
        {
            if (!string.IsNullOrWhiteSpace(profile))
                _parameters.Add($"--profile {profile}");
            return this;
        }

        /// <summary>
        /// Sets the intra parameter (for example, 30).
        /// </summary>
        public RpiCameraCommandBuilder SetIntra(int intra)
        {
            _parameters.Add($"--intra {intra}");
            return this;
        }

        /// <summary>
        /// Sets the sharpness value.
        /// </summary>
        public RpiCameraCommandBuilder SetSharpness(double sharpness)
        {
            _parameters.Add($"--sharpness {sharpness.ToString(CultureInfo.InvariantCulture)}");
            return this;
        }

        /// <summary>
        /// Sets the gain value.
        /// </summary>
        public RpiCameraCommandBuilder SetGain(double gain)
        {
            _parameters.Add($"--gain {gain.ToString(CultureInfo.InvariantCulture)}");
            return this;
        }

        /// <summary>
        /// Sets the shutter time in microseconds.
        /// </summary>
        public RpiCameraCommandBuilder SetShutter(int shutter)
        {
            _parameters.Add($"--shutter {shutter}");
            return this;
        }

        /// <summary>
        /// Sets the AWB gains using comma-separated values (red,blue).
        /// </summary>
        public RpiCameraCommandBuilder SetAwbGains(double redGain, double blueGain)
        {
            string gains = $"{redGain.ToString(CultureInfo.InvariantCulture)},{blueGain.ToString(CultureInfo.InvariantCulture)}";
            _parameters.Add($"--awbgains {gains}");
            return this;
        }

        /// <summary>
        /// Sets the metering mode (e.g. "average").
        /// </summary>
        public RpiCameraCommandBuilder SetMetering(string metering)
        {
            if (!string.IsNullOrWhiteSpace(metering))
                _parameters.Add($"--metering {metering}");
            return this;
        }

        /// <summary>
        /// Sets the brightness value.
        /// </summary>
        public RpiCameraCommandBuilder SetBrightness(double brightness)
        {
            _parameters.Add($"--brightness {brightness.ToString(CultureInfo.InvariantCulture)}");
            return this;
        }

        /// <summary>
        /// Sets the contrast value.
        /// </summary>
        public RpiCameraCommandBuilder SetContrast(double contrast)
        {
            _parameters.Add($"--contrast {contrast.ToString(CultureInfo.InvariantCulture)}");
            return this;
        }

        /// <summary>
        /// Sets the saturation value.
        /// </summary>
        public RpiCameraCommandBuilder SetSaturation(double saturation)
        {
            _parameters.Add($"--saturation {saturation.ToString(CultureInfo.InvariantCulture)}");
            return this;
        }

        /// <summary>
        /// Sets a region of interest (ROI) using normalized coordinates: x, y, width, height.
        /// </summary>
        public RpiCameraCommandBuilder SetROI(double x, double y, double w, double h)
        {
            string roiValue = string.Format(CultureInfo.InvariantCulture, "{0:F4},{1:F4},{2:F4},{3:F4}", x, y, w, h);
            _parameters.Add($"--roi {roiValue}");
            return this;
        }

        /// <summary>
        /// Helper to automatically set ROI for a given digital zoom factor.
        /// For a zoom factor Z, the ROI is set to ((1-1/Z)/2, (1-1/Z)/2, 1/Z, 1/Z).
        /// </summary>
        public RpiCameraCommandBuilder SetDigitalZoom(double zoomFactor)
        {
            if (zoomFactor < 1)
                throw new ArgumentException("Zoom factor must be >= 1", nameof(zoomFactor));
            double cropFraction = 1.0 / zoomFactor;
            double offset = (1.0 - cropFraction) / 2.0;
            return SetROI(offset, offset, cropFraction, cropFraction);
        }        

        /// <summary>
        /// Adds any custom parameter string.
        /// </summary>
        public RpiCameraCommandBuilder AddParameter(string parameter)
        {
            if (!string.IsNullOrWhiteSpace(parameter))
                _parameters.Add(parameter);
            return this;
        }

        /// <summary>
        /// Builds and returns the full command-line string.
        /// </summary>
        public string BuildCommandLine()
        {
            return $"{BaseCommand} {string.Join(" ", _parameters)}";
        }
    }
}
