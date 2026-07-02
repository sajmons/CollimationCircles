using CollimationCircles.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    /// <summary>
    /// Provides direct frame delivery from a UVC camera via libusb, bypassing LibVLC.
    /// The <see cref="FrameReady"/> event fires for every captured frame with
    /// JPEG-encoded image data.
    /// </summary>
    internal interface IUvcFrameSource
    {
        /// <summary>True while a UVC camera stream is active.</summary>
        bool IsStreaming { get; }

        /// <summary>Width of the captured frame in pixels (0 until stream starts).</summary>
        int FrameWidth { get; }

        /// <summary>Height of the captured frame in pixels (0 until stream starts).</summary>
        int FrameHeight { get; }

        /// <summary>
        /// Fired on the capture thread for every new frame. The byte array
        /// contains JPEG-encoded image data of size <see cref="FrameWidth"/> × <see cref="FrameHeight"/>.
        /// </summary>
        event Action<byte[], int, int>? FrameReady;

        /// <summary>Opens the UVC camera and starts the capture loop.</summary>
        Task<bool> StartAsync(Camera camera);

        /// <summary>Stops the capture loop and closes the camera.</summary>
        void Stop();

        /// <summary>Sets a UVC control value on the active stream.</summary>
        bool SetControl(string controlName, long value);

        /// <summary>Sets a UVC auto control value on the active stream.</summary>
        bool SetAutoControl(string controlName, bool isAuto);

        /// <summary>Enumerates all supported UVC controls (requires device to be open).</summary>
        List<ICameraControl> EnumerateControls(Camera camera);
    }
}