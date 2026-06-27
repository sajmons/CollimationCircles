using CollimationCircles.Models;
using System;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    /// <summary>
    /// Adapter that wraps <see cref="ZWOLiveStreamService"/> in direct-rendering
    /// mode and exposes it through <see cref="IZwoFrameSource"/>.
    /// Registered as a singleton so <c>StreamView</c> and <c>LibVLCService</c>
    /// share the same instance.
    /// </summary>
    internal sealed class ZwoFrameSource : IZwoFrameSource, IDisposable
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private ZWOLiveStreamService? _stream;

        public bool IsStreaming => _stream != null;

        public int FrameWidth => _stream?.FrameWidth ?? 0;
        public int FrameHeight => _stream?.FrameHeight ?? 0;

        public event Action<byte[], int, int>? FrameReady;

        public async Task<bool> StartAsync(Camera camera)
        {
            Stop();

            _stream = new ZWOLiveStreamService { EnableDirectRendering = true };
            _stream.FrameReady += OnFrameReady;

            bool started = await _stream.StartAsync(camera);
            if (!started)
            {
                logger.Error($"Failed to start ZWO direct stream for camera '{camera.Name}'");
                _stream.FrameReady -= OnFrameReady;
                _stream = null;
            }

            return started;
        }

        public void Stop()
        {
            if (_stream is null) return;

            _stream.FrameReady -= OnFrameReady;
            _stream.Stop();
            _stream.Dispose();
            _stream = null;
        }

        private void OnFrameReady(byte[] frame, int width, int height)
        {
            FrameReady?.Invoke(frame, width, height);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}