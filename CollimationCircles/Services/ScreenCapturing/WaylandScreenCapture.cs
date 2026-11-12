using System;

namespace CollimationCircles.Services.ScreenCapturing
{
    /// <summary>
    /// Captures the screen in Wayland sessions. The desktop portal screen cast is preferred; when the desktop
    /// has no portal backend for it (typical for wlroots-based desktops) the grim tool is used instead.
    /// </summary>
    internal sealed class WaylandScreenCapture : IScreenCaptureBackend, IDisposable
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private IScreenCaptureBackend? inner;
        private bool disposed;

        public string Name => inner?.Name ?? "Wayland";

        public ScreenCaptureFrame Capture(CaptureRegion region)
        {
            if (disposed)
            {
                throw new OperationCanceledException();
            }

            inner ??= new WaylandPortalScreenCapture();

            try
            {
                return inner.Capture(region);
            }
            catch (ScreenCastUnavailableException ex) when (inner is WaylandPortalScreenCapture)
            {
                logger.Warn($"Screen cast portal is unavailable, falling back to grim. {ex.Message}");

                (inner as IDisposable)?.Dispose();
                inner = new GrimScreenCapture();
                return inner.Capture(region);
            }
        }

        public void Dispose()
        {
            disposed = true;
            (inner as IDisposable)?.Dispose();
        }
    }
}
