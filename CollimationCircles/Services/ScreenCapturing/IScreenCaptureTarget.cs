using Avalonia;

namespace CollimationCircles.Services.ScreenCapturing
{
    /// <summary>
    /// A window whose client area defines the screen region that is captured while the
    /// screen capture camera is running.
    /// </summary>
    public interface IScreenCaptureTarget
    {
        /// <summary>
        /// Gets the region of the window to capture in physical screen pixels. Must be called on the UI thread.
        /// </summary>
        PixelRect GetCaptureRect();
    }
}
