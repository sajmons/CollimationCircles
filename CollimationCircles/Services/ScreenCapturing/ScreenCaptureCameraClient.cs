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

namespace CollimationCircles.Services.ScreenCapturing
{
    /// <summary>
    /// Camera that captures the screen content behind the application windows.
    /// Pixels are read directly from the operating system as raw BGRA data and handed to the analysis as a Mat.
    /// The capture region is the stream window when it is visible and active, otherwise the main window.
    /// </summary>
    internal class ScreenCaptureCameraClient
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private const int FrameIntervalMs = 250;

        private readonly CaptureExclusion exclusion = new();
        private CancellationTokenSource? cts;
        private Task? loop;

        public async Task Start(Camera camera)
        {
            await Stop();

            var backend = ScreenCaptureBackendFactory.Create();
            logger.Info($"Starting screen capture camera (backend: {backend.Name})");

            cts = new CancellationTokenSource();
            var token = cts.Token;
            loop = Task.Run(() => CaptureLoop(backend, token));
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

        private async Task CaptureLoop(IScreenCaptureBackend backend, CancellationToken ct)
        {
            try
            {
                var stopwatch = new Stopwatch();

                while (!ct.IsCancellationRequested)
                {
                    stopwatch.Restart();

                    var region = await Dispatcher.UIThread.InvokeAsync(ResolveCaptureRegion);

                    if (region is { Bounds: { Width: > 0, Height: > 0 } })
                    {
                        using var frame = ToMat(backend.Capture(region));
                        if (!ct.IsCancellationRequested)
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

        private static Mat ToMat(ScreenCaptureFrame frame)
        {
            var handle = GCHandle.Alloc(frame.Pixels, GCHandleType.Pinned);
            try
            {
                // Stride may include row padding, so it must be passed explicitly.
                using var source = Mat.FromPixelData(frame.Height, frame.Width, MatType.CV_8UC4, handle.AddrOfPinnedObject(), frame.Stride);
                var bgr = new Mat();
                Cv2.CvtColor(source, bgr, ColorConversionCodes.BGRA2BGR);
                return bgr;
            }
            finally
            {
                handle.Free();
            }
        }

        private CaptureRegion? ResolveCaptureRegion()
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime lifetime)
            {
                return null;
            }

            var windows = lifetime.Windows;
            exclusion.Apply(windows);

            Avalonia.Controls.Window? window = windows
                .OfType<StreamView>()
                .FirstOrDefault(w => w.IsVisible && w.IsActive && w.WindowState != WindowState.Minimized);

            window ??= windows
                .OfType<MainView>()
                .FirstOrDefault(w => w.IsVisible && w.WindowState != WindowState.Minimized);

            if (window is not IScreenCaptureTarget target)
            {
                return null;
            }

            var bounds = target.GetCaptureRect();
            var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;

            return screen is null
                ? new CaptureRegion(bounds, bounds, window.RenderScaling, null)
                : new CaptureRegion(bounds, screen.Bounds, screen.Scaling, screen.DisplayName);
        }
    }
}
