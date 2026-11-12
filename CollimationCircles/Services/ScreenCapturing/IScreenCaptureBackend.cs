using Avalonia;
using System;

namespace CollimationCircles.Services.ScreenCapturing
{
    /// <summary>
    /// The screen rectangle to capture, in physical pixels, together with the monitor it was selected on.
    /// </summary>
    /// <param name="Bounds">Capture rectangle in virtual screen pixels.</param>
    /// <param name="MonitorBounds">Bounds of the monitor containing the rectangle, in virtual screen pixels.</param>
    /// <param name="Scaling">Render scaling of that monitor (1.0 = 100%).</param>
    /// <param name="MonitorName">Display name of that monitor, when the platform reports one.</param>
    internal sealed record CaptureRegion(PixelRect Bounds, PixelRect MonitorBounds, double Scaling, string? MonitorName);

    /// <summary>A captured frame as raw 8-bit BGRA pixels. Stride may be larger than Width * 4.</summary>
    internal sealed record ScreenCaptureFrame(byte[] Pixels, int Width, int Height, int Stride);

    /// <summary>
    /// Reads pixels of a screen region directly from the operating system, without encoding or temporary files.
    /// </summary>
    internal interface IScreenCaptureBackend
    {
        string Name { get; }

        ScreenCaptureFrame Capture(CaptureRegion region);
    }

    internal static class ScreenCaptureBackendFactory
    {
        public static IScreenCaptureBackend Create()
        {
            if (OperatingSystem.IsWindows())
            {
                return new WindowsScreenCapture();
            }

            if (OperatingSystem.IsMacOS())
            {
                return new MacOsScreenCapture();
            }

            if (OperatingSystem.IsLinux())
            {
                if (string.Equals(Environment.GetEnvironmentVariable("XDG_SESSION_TYPE"), "wayland", StringComparison.OrdinalIgnoreCase))
                {
                    return new WaylandPortalScreenCapture();
                }

                return new X11ScreenCapture();
            }

            throw new PlatformNotSupportedException("Screen capture is not supported on this platform.");
        }
    }
}
