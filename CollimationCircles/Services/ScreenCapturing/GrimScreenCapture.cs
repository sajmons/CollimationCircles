using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace CollimationCircles.Services.ScreenCapturing
{
    /// <summary>
    /// Captures the screen on wlroots-based Wayland compositors (Sway, Wayfire, labwc, as on Raspberry Pi OS)
    /// with the grim tool, which uses the wlr-screencopy protocol. It needs no permission prompt and makes no sound.
    /// </summary>
    internal sealed class GrimScreenCapture : IScreenCaptureBackend
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        public string Name => "Wayland (grim)";

        public ScreenCaptureFrame Capture(CaptureRegion region)
        {
            var scale = region.Scaling > 0 ? region.Scaling : 1.0;
            var bounds = region.Bounds;

            // grim takes the geometry in logical coordinates and renders at the output scale given with -s.
            var x = (int)Math.Round(bounds.X / scale);
            var y = (int)Math.Round(bounds.Y / scale);
            var width = Math.Max(1, (int)Math.Round(bounds.Width / scale));
            var height = Math.Max(1, (int)Math.Round(bounds.Height / scale));

            var startInfo = new ProcessStartInfo("grim")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var argument in new[]
            {
                "-t", "ppm",
                "-s", scale.ToString("0.###", CultureInfo.InvariantCulture),
                "-g", $"{x},{y} {width}x{height}",
                "-"
            })
            {
                startInfo.ArgumentList.Add(argument);
            }

            Process process;
            try
            {
                process = Process.Start(startInfo) ?? throw new InvalidOperationException("grim could not be started.");
            }
            catch (Win32Exception ex)
            {
                throw new InvalidOperationException(
                    "Screen capture on Wayland needs the grim tool. Install it with: sudo apt install grim", ex);
            }

            using (process)
            {
                var errors = process.StandardError.ReadToEndAsync();
                using var output = new MemoryStream();
                process.StandardOutput.BaseStream.CopyTo(output);

                if (!process.WaitForExit(Timeout))
                {
                    process.Kill(true);
                    throw new TimeoutException("grim did not finish in time.");
                }

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        $"grim failed (exit code {process.ExitCode}): {errors.GetAwaiter().GetResult().Trim()}. " +
                        "grim only works on wlroots-based Wayland desktops (Sway, Wayfire, labwc).");
                }

                return ParsePpm(output.GetBuffer().AsSpan(0, (int)output.Length));
            }
        }

        private static ScreenCaptureFrame ParsePpm(ReadOnlySpan<byte> data)
        {
            var position = 0;
            var magic = ReadToken(data, ref position);
            if (magic != "P6")
            {
                throw new InvalidOperationException("grim returned an unexpected image format.");
            }

            var width = int.Parse(ReadToken(data, ref position), CultureInfo.InvariantCulture);
            var height = int.Parse(ReadToken(data, ref position), CultureInfo.InvariantCulture);
            var maxValue = int.Parse(ReadToken(data, ref position), CultureInfo.InvariantCulture);
            if (maxValue != 255)
            {
                throw new InvalidOperationException("grim returned an unsupported color depth.");
            }

            // A single whitespace byte separates the header from the pixel data.
            position++;

            if (width <= 0 || height <= 0 || data.Length - position < (long)width * height * 3)
            {
                throw new InvalidOperationException("grim returned an incomplete image.");
            }

            var stride = width * 4;
            var pixels = new byte[checked(stride * height)];
            var source = data[position..];
            for (int i = 0, s = 0, d = 0; i < width * height; i++, s += 3, d += 4)
            {
                pixels[d] = source[s + 2];
                pixels[d + 1] = source[s + 1];
                pixels[d + 2] = source[s];
                pixels[d + 3] = 255;
            }

            return new ScreenCaptureFrame(pixels, width, height, stride);
        }

        private static string ReadToken(ReadOnlySpan<byte> data, ref int position)
        {
            while (position < data.Length && char.IsWhiteSpace((char)data[position]))
            {
                position++;
            }

            var start = position;
            while (position < data.Length && !char.IsWhiteSpace((char)data[position]))
            {
                position++;
            }

            return Encoding.ASCII.GetString(data[start..position]);
        }
    }
}
