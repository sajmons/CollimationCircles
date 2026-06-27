using CollimationCircles.Models;
using System;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    /// <summary>
    /// Provides direct frame delivery from a ZWO ASI camera, bypassing LibVLC.
    /// The <see cref="FrameReady"/> event fires for every captured frame with
    /// JPEG-encoded image data.
    /// </summary>
    internal interface IZwoFrameSource
    {
        /// <summary>True while a ZWO camera stream is active.</summary>
        bool IsStreaming { get; }

        /// <summary>Width of the captured ROI in pixels (0 until stream starts).</summary>
        int FrameWidth { get; }

        /// <summary>Height of the captured ROI in pixels (0 until stream starts).</summary>
        int FrameHeight { get; }

        /// <summary>
        /// Fired on the capture thread for every new frame. The byte array
        /// contains JPEG-encoded image data of size <see cref="FrameWidth"/> × <see cref="FrameHeight"/>.
        /// </summary>
        event Action<byte[], int, int>? FrameReady;

        /// <summary>Opens the ZWO camera and starts the capture loop.</summary>
        Task<bool> StartAsync(Camera camera);

        /// <summary>Stops the capture loop and closes the camera.</summary>
        void Stop();
    }
}