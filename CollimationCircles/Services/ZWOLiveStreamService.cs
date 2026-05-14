using CollimationCircles.Models;
using NLog;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    /// <summary>
    /// Captures frames from a ZWO ASI camera using the ASI SDK and serves them
    /// as an MJPEG stream over a local HTTP listener.  LibVLC then plays the
    /// resulting http://localhost:{Port}/ URL.
    /// </summary>
    internal sealed class ZWOLiveStreamService : IDisposable
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // Tracks all currently-running streams keyed by ASI camera ID so that
        // ZWOCameraDetect.SetControl can detect whether a camera is open.
        private static readonly Dictionary<int, ZWOLiveStreamService> _activeStreams = [];
        private static readonly object _activeStreamsLock = new();

        private int _cameraId;
        private int _width;
        private int _height;

        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _captureTask;
        private Task? _serverTask;

        private byte[]? _latestJpeg;
        private readonly object _frameLock = new();

        private bool _disposed;
        private bool _running;

        public int Port { get; private set; }

        // ------------------------------------------------------------------ //
        //  Public static helpers                                               //
        // ------------------------------------------------------------------ //

        /// <summary>Returns true while the given ASI camera ID has an active live stream.</summary>
        public static bool IsStreaming(int cameraId)
        {
            lock (_activeStreamsLock)
            {
                return _activeStreams.ContainsKey(cameraId);
            }
        }

        // ------------------------------------------------------------------ //
        //  Lifecycle                                                           //
        // ------------------------------------------------------------------ //

        public async Task<bool> StartAsync(Camera camera)
        {
            if (!int.TryParse(camera.Path, out int cameraId))
            {
                logger.Error($"ZWOLiveStreamService: invalid camera ID in Path '{camera.Path}'");
                return false;
            }

            _cameraId = cameraId;

            // Query the camera for its maximum resolution
            var info = new ZWOASICameraInterop.ASI_CAMERA_INFO();
            ZWOASICameraInterop.ASIGetCameraProperty(ref info, camera.Index);

            // Cap at 1280×960 for a responsive preview; must be multiples of 8
            _width  = (Math.Min((int)info.MaxWidth,  1280) / 8) * 8;
            _height = (Math.Min((int)info.MaxHeight,  960) / 8) * 8;

            if (_width <= 0 || _height <= 0)
            {
                // Fallback if resolution info is unavailable
                _width  = 640;
                _height = 480;
            }

            logger.Info($"ZWO stream: opening camera {_cameraId}, ROI {_width}×{_height}");

            // Open and initialise the camera
            int r = ZWOASICameraInterop.ASIOpenCamera(_cameraId);
            if (r != 0) { logger.Error($"ASIOpenCamera failed ({r})"); return false; }

            r = ZWOASICameraInterop.ASIInitCamera(_cameraId);
            if (r != 0)
            {
                logger.Error($"ASIInitCamera failed ({r})");
                ZWOASICameraInterop.ASICloseCamera(_cameraId);
                return false;
            }

            // Apply startup policy on every Play:
            // - Gain: manual mode with default gain value.
            // - Exposure: auto mode.
            ApplyStartupControls(camera);

            // Y8 = 8-bit luminance (grayscale) — works on all ASI cameras
            r = ZWOASICameraInterop.ASISetROIFormat(_cameraId, _width, _height, 1,
                ZWOASICameraInterop.ASI_IMG_TYPE.ASI_IMG_Y8);
            if (r != 0)
            {
                logger.Error($"ASISetROIFormat failed ({r})");
                ZWOASICameraInterop.ASICloseCamera(_cameraId);
                return false;
            }

            r = ZWOASICameraInterop.ASIStartVideoCapture(_cameraId);
            if (r != 0)
            {
                logger.Error($"ASIStartVideoCapture failed ({r})");
                ZWOASICameraInterop.ASICloseCamera(_cameraId);
                return false;
            }

            // Bind an HTTP listener on a free local port
            Port = FindFreePort();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{Port}/");
            _listener.Start();

            _cts = new CancellationTokenSource();
            _captureTask = Task.Run(() => CaptureLoop(_cts.Token));
            _serverTask  = Task.Run(() => ServerLoop(_cts.Token));

            lock (_activeStreamsLock)
            {
                _activeStreams[_cameraId] = this;
            }

            _running = true;

            logger.Info($"ZWO MJPEG stream running at http://localhost:{Port}/  (camera {_cameraId})");
            return await Task.FromResult(true);
        }

        private void ApplyStartupControls(Camera camera)
        {
            try
            {
                ICameraControl? gainControl = camera.Controls.FirstOrDefault(c => c.Name == ControlType.Gain);
                ICameraControl? exposureControl = camera.Controls.FirstOrDefault(c => c.Name == ControlType.ExposureTime);

                int gainValue = gainControl?.Default ?? gainControl?.Value ?? 100;
                long exposureValue = exposureControl?.Value ?? exposureControl?.Default ?? 100;

                int gainResult = ZWOASICameraInterop.ASISetControlValue(
                    _cameraId,
                    (int)ZWOASICameraInterop.ASI_CONTROL_TYPE.ASI_GAIN,
                    gainValue,
                    0);

                if (gainResult != (int)ZWOASICameraInterop.ASI_ERROR_CODE.ASI_SUCCESS)
                {
                    logger.Warn($"Failed to apply startup gain={gainValue} (manual) on camera {_cameraId}, error: {gainResult}");
                }
                else
                {
                    logger.Info($"Applied startup gain={gainValue} (manual) on camera {_cameraId}");
                }

                int exposureResult = ZWOASICameraInterop.ASISetControlValue(
                    _cameraId,
                    (int)ZWOASICameraInterop.ASI_CONTROL_TYPE.ASI_EXPOSURE,
                    exposureValue,
                    1);

                if (exposureResult != (int)ZWOASICameraInterop.ASI_ERROR_CODE.ASI_SUCCESS)
                {
                    logger.Warn($"Failed to apply startup exposure auto on camera {_cameraId}, error: {exposureResult}");
                }
                else
                {
                    logger.Info($"Applied startup exposure auto on camera {_cameraId}");
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"Failed to apply startup controls for camera {_cameraId}");
            }
        }

        public void Stop()
        {
            if (_disposed || !_running) return;

            _running = false;

            _cts?.Cancel();

            // Stop SDK capture and close camera
            try
            {
                ZWOASICameraInterop.ASIStopVideoCapture(_cameraId);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "ASIStopVideoCapture error during stop");
            }

            try
            {
                ZWOASICameraInterop.ASICloseCamera(_cameraId);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "ASICloseCamera error during stop");
            }

            _listener?.Stop();

            try { _captureTask?.Wait(2000); } catch { }
            try { _serverTask?.Wait(2000); } catch { }

            lock (_activeStreamsLock)
            {
                _activeStreams.Remove(_cameraId);
            }

            logger.Info($"ZWO live stream stopped for camera {_cameraId}");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Stop();
                _cts?.Dispose();
                _cts = null;
                _disposed = true;
            }
        }

        // ------------------------------------------------------------------ //
        //  Frame capture                                                       //
        // ------------------------------------------------------------------ //

        private void CaptureLoop(CancellationToken ct)
        {
            int bufferSize = _width * _height; // Y8 = 1 byte per pixel
            IntPtr nativeBuffer = Marshal.AllocHGlobal(bufferSize);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    // Wait up to 2 000 ms for the next frame
                    int result = ZWOASICameraInterop.ASIGetVideoData(
                        _cameraId, nativeBuffer, bufferSize, 2000);

                    if (result != 0)
                    {
                        // Timeout or transient error – just retry
                        continue;
                    }

                    byte[] raw = new byte[bufferSize];
                    Marshal.Copy(nativeBuffer, raw, 0, bufferSize);

                    byte[] jpeg = EncodeGrayscaleToJpeg(raw, _width, _height);

                    lock (_frameLock)
                    {
                        _latestJpeg = jpeg;
                    }
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.Error(ex, "ZWO capture loop error");
            }
            finally
            {
                Marshal.FreeHGlobal(nativeBuffer);
            }
        }

        private static byte[] EncodeGrayscaleToJpeg(byte[] rawY8, int width, int height)
        {
            using var image = Image.LoadPixelData<L8>(rawY8, width, height);
            using var ms = new MemoryStream();
            image.Save(ms, new JpegEncoder { Quality = 80 });
            return ms.ToArray();
        }

        // ------------------------------------------------------------------ //
        //  MJPEG HTTP server                                                  //
        // ------------------------------------------------------------------ //

        private async Task ServerLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && (_listener?.IsListening == true))
            {
                try
                {
                    var ctx = await _listener!.GetContextAsync();
                    _ = Task.Run(() => HandleClient(ctx, ct), ct);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    logger.Warn(ex, "ZWO MJPEG server loop error");
                }
            }
        }

        private async Task HandleClient(HttpListenerContext ctx, CancellationToken ct)
        {
            const string boundary = "zwoframe";

            ctx.Response.ContentType  = $"multipart/x-mixed-replace; boundary={boundary}";
            ctx.Response.StatusCode   = 200;
            ctx.Response.SendChunked  = true;

            Stream stream = ctx.Response.OutputStream;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    byte[]? jpeg;
                    lock (_frameLock)
                    {
                        jpeg = _latestJpeg;
                    }

                    if (jpeg is not null)
                    {
                        // Write MJPEG part header as ASCII, then the binary JPEG payload
                        string header = $"--{boundary}\r\nContent-Type: image/jpeg\r\nContent-Length: {jpeg.Length}\r\n\r\n";
                        byte[] headerBytes  = Encoding.ASCII.GetBytes(header);
                        byte[] trailerBytes = Encoding.ASCII.GetBytes("\r\n");

                        await stream.WriteAsync(headerBytes, ct);
                        await stream.WriteAsync(jpeg,        ct);
                        await stream.WriteAsync(trailerBytes, ct);
                        await stream.FlushAsync(ct);
                    }

                    // Target roughly 30 fps; yield CPU when no frame is available yet
                    await Task.Delay(33, ct);
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                logger.Debug(ex, "ZWO MJPEG client disconnected");
            }
            finally
            {
                try { ctx.Response.OutputStream.Close(); } catch { }
            }
        }

        // ------------------------------------------------------------------ //
        //  Helpers                                                             //
        // ------------------------------------------------------------------ //

        private static int FindFreePort()
        {
            // RasPi stream uses 8090; probe from 8091 upwards
            for (int p = 8091; p <= 8199; p++)
            {
                try
                {
                    var probe = new TcpListener(IPAddress.Loopback, p);
                    probe.Start();
                    probe.Stop();
                    return p;
                }
                catch { /* port in use, try next */ }
            }

            return 8091; // last resort
        }
    }
}
