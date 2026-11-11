using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CollimationCircles.Messages;
using CollimationCircles.Models;
using CollimationCircles.Views;
using CommunityToolkit.Mvvm.Messaging;
using OpenCvSharp;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ThatCapture;

namespace CollimationCircles.Services.ScreenCapturing
{
    /// <summary>
    /// Camera that captures the screen content behind the application windows.
    /// Uses native capture on Windows and ThatCapture on other platforms.
    /// The capture region is the stream window when it is visible and active, otherwise the main window.
    /// </summary>
    internal class ScreenCaptureCameraClient
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private const int FrameIntervalMs = 100;

        private readonly WindowsScreenCapture? windowsCapture = OperatingSystem.IsWindows() ? new() : null;
        private readonly ThatCapture.ScreenCapture? capture = OperatingSystem.IsWindows() ? null : new();
        private readonly CaptureExclusion exclusion = new();
        private CancellationTokenSource? cts;
        private Task? loop;

        public async Task Start(Camera camera)
        {
            await Stop();

            var platform = windowsCapture is not null ? "Windows" : capture!.Platform.ToString();
            logger.Info($"Starting screen capture camera (platform: {platform})");

            cts = new CancellationTokenSource();
            var token = cts.Token;
            loop = Task.Run(() => CaptureLoop(token));
        }

        public async Task Stop()
        {
            var source = cts;
            var task = loop;
            cts = null;
            loop = null;

            if (source is null)
            {
                return;
            }

            source.Cancel();

            try
            {
                if (task is not null)
                {
                    await task;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                logger.Warn($"Screen capture loop ended with error: {ex.Message}");
            }

            await Dispatcher.UIThread.InvokeAsync(exclusion.Restore);

            source.Dispose();
            logger.Info("Stopped screen capture camera");
        }

        private async Task CaptureLoop(CancellationToken ct)
        {
            try
            {
                var stopwatch = new Stopwatch();

                while (!ct.IsCancellationRequested)
                {
                    stopwatch.Restart();

                    var rect = await Dispatcher.UIThread.InvokeAsync(ResolveCaptureRect);

                    if (rect is { Width: > 0, Height: > 0 } r)
                    {
                        using var frame = await CaptureFrameAsync(r);
                        if (frame is not null && !ct.IsCancellationRequested)
                        {
                            // The captured screen is already visible behind the stream window, so only the overlay is drawn.
                            WeakReferenceMessenger.Default.Send(new MatMessage(frame, display: false));
                        }
                    }

                    var delay = FrameIntervalMs - (int)stopwatch.ElapsedMilliseconds;
                    if (delay > 0)
                    {
                        await Task.Delay(delay, ct);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Screen capture loop failed");
                Dispatcher.UIThread.Post(() =>
                    WeakReferenceMessenger.Default.Send(new CameraStateMessage(CameraState.Stopped)));
            }
        }

        private async Task<Mat?> CaptureFrameAsync(PixelRect rect)
        {
            if (windowsCapture is not null)
            {
                return ToMat(windowsCapture.CaptureArea(rect.X, rect.Y, rect.Width, rect.Height));
            }

            var result = await capture!.CaptureAreaAsync(rect.X, rect.Y, rect.Width, rect.Height);
            switch (result)
            {
                case CaptureResult.Ok(var captured):
                    return ToMat(captured);
                case CaptureResult.Err(var error):
                    throw new InvalidOperationException($"Screen capture failed: {error}");
                default:
                    throw new InvalidOperationException("Screen capture returned an unsupported result.");
            }
        }

        private static Mat? ToMat(CapturedFrame frame)
        {
            return ToMat(frame.Pixels, frame.Width, frame.Height, frame.Stride, frame.Format);
        }

        private static Mat? ToMat(ScreenCaptureFrame frame)
        {
            return ToMat(frame.Pixels, frame.Width, frame.Height, frame.Stride, ThatCapture.PixelFormat.Bgra8888);
        }

        private static Mat? ToMat(byte[] pixels, int width, int height, int stride, ThatCapture.PixelFormat format)
        {
            var conversion = format switch
            {
                ThatCapture.PixelFormat.Bgra8888 => ColorConversionCodes.BGRA2BGR,
                ThatCapture.PixelFormat.Rgba8888 => ColorConversionCodes.RGBA2BGR,
                _ => (ColorConversionCodes?)null
            };

            if (conversion is null)
            {
                logger.Warn($"Unsupported screen capture pixel format: {format}");
                return null;
            }

            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                // Stride may include row padding, so it must be passed explicitly.
                using var source = Mat.FromPixelData(height, width, MatType.CV_8UC4, handle.AddrOfPinnedObject(), stride);
                var bgr = new Mat();
                Cv2.CvtColor(source, bgr, conversion.Value);
                return bgr;
            }
            finally
            {
                handle.Free();
            }
        }

        private PixelRect? ResolveCaptureRect()
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime)
            {
                return null;
            }

            var windows = lifetime.Windows;
            exclusion.Apply(windows);

            IScreenCaptureTarget? target = windows
                .OfType<StreamView>()
                .FirstOrDefault(w => w.IsVisible && w.IsActive && w.WindowState != WindowState.Minimized);

            target ??= windows
                .OfType<MainView>()
                .FirstOrDefault(w => w.IsVisible && w.WindowState != WindowState.Minimized);

            return target?.GetCaptureRect();
        }
    }
}
