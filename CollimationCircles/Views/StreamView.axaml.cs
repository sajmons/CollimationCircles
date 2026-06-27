using Avalonia;
using Avalonia.Controls;
using CollimationCircles.Messages;
using CollimationCircles.Services;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using LibVLCSharp.Avalonia;
using System;

namespace CollimationCircles.Views
{
    public partial class StreamView : Window
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private const double MinZoom = 0.5;
        private const double MaxZoom = 4.0;
        private const double ZoomStep = 0.2;

        private readonly VideoView videoViewer;
        private readonly SettingsViewModel svm;
        private double currentZoom = 1.0;
        private int sourceVideoWidth;
        private int sourceVideoHeight;
        private bool sourceDimensionsCaptured = false;
        private bool vlcCropCleared = false;

        public StreamView()
        {
            InitializeComponent();
            svm = Ioc.Default.GetRequiredService<SettingsViewModel>();
            DataContext = Ioc.Default.GetRequiredService<CollimationAnalysisViewModel>();

            videoViewer = this.Get<VideoView>("VideoViewer");

            WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) =>
            {
                // FIXME: here is probably some room for optimization
                UpdateWindowPosition();
            });

            WeakReferenceMessenger.Default.Register<ImageZoomMessage>(this, (r, m) =>
            {
                ApplyZoom(m.Value);
            });

            Opened += WebCamStreamWindow_Opened;
            Closed += StreamView_Closed;
        }

        private void StreamView_Closed(object? sender, EventArgs e)
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);
        }

        private void WebCamStreamWindow_Opened(object? sender, System.EventArgs e)
        {
            var mp = Ioc.Default.GetRequiredService<ILibVLCService>().MediaPlayer;

            if (videoViewer != null)
            {
                if (mp is not null)
                {
                    videoViewer.MediaPlayer = mp;

                    logger.Info($"StreamView opened. MediaPlayer IsPlaying={mp.IsPlaying}, CropGeometry='{mp.CropGeometry}', Scale={mp.Scale}");

                    // Capture source dimensions once when stream starts playing.
                    // We retry on every Playing event until we get non-zero values,
                    // because mp.Size(0,...) can return 0,0 before the first decoded
                    // frame is available.
                    mp.Playing += (s, ev) =>
                    {
                        if (!sourceDimensionsCaptured)
                        {
                            if (TryGetVideoSize(mp, out int w, out int h))
                            {
                                sourceVideoWidth = w;
                                sourceVideoHeight = h;
                                sourceDimensionsCaptured = true;
                                logger.Info($"Captured source video dimensions from Playing event: {w}x{h}");
                            }
                            else
                            {
                                logger.Debug("Playing event fired but mp.Size(0) returned 0x0; will retry on next event.");
                            }
                        }
                    };
                }

                currentZoom = 1.0;
                sourceVideoWidth = 0;
                sourceVideoHeight = 0;
                sourceDimensionsCaptured = false;
                vlcCropCleared = false;
                UpdateImageTransform();
                UpdateWindowPosition();
            }
        }

        private void ApplyZoom(ImageZoomAction action)
        {
            double prevZoom = currentZoom;

            switch (action)
            {
                case ImageZoomAction.In:
                    ApplyZoomDelta(ZoomStep);
                    break;
                case ImageZoomAction.Out:
                    ApplyZoomDelta(-ZoomStep);
                    break;
                case ImageZoomAction.Reset:
                    currentZoom = 1.0;
                    break;
            }

            logger.Info($"ApplyZoom: action={action}, zoom {prevZoom:F2} -> {currentZoom:F2}");

            UpdateImageTransform();
        }

        private void UpdateImageTransform()
        {
            var mp = videoViewer.MediaPlayer;

            if (mp is null)
            {
                logger.Warn("UpdateImageTransform: MediaPlayer is null, skipping.");
                return;
            }

            if (currentZoom <= 1.0)
            {
                // Clear LibVLC crop/scale only once when returning to zoom=1.0,
                // to avoid triggering a vout reconfiguration (flicker) on every call.
                if (!vlcCropCleared)
                {
                    mp.CropGeometry = string.Empty;
                    mp.Scale = 0;
                    vlcCropCleared = true;
                }

                // Reset VideoView to fill the window
                videoViewer.Width = double.NaN;
                videoViewer.Height = double.NaN;
                videoViewer.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                videoViewer.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;

                logger.Info($"UpdateImageTransform: zoom<=1, reset VideoView to stretch (zoom={currentZoom:F2}).");
                return;
            }

            // NOTE: On macOS VLC 3.0.x, CropGeometry "+X+Y" offset is ignored
            // (crops from 0,0 → diagonal drift), Scale re-fits instead of clipping,
            // and Avalonia RenderTransform has no effect on the native overlay.
            //
            // WORKAROUND: Resize the VideoView itself to be larger than the window
            // and keep it centered.  The parent Grid's ClipToBounds clips the
            // overflow, so only the center region of the video is visible.
            // This is center-based zoom by construction — no VLC crop API needed.
            //
            // The VideoView's native overlay (NSView) resizes with the Avalonia
            // control, and VLC auto-fits the video into the larger VideoView, so
            // the video scales up.  The window clips what doesn't fit.

            if (!sourceDimensionsCaptured && (sourceVideoWidth <= 0 || sourceVideoHeight <= 0))
            {
                bool ok = TryGetVideoSize(mp, out sourceVideoWidth, out sourceVideoHeight);
                sourceDimensionsCaptured = true;
                logger.Info($"UpdateImageTransform: late dimension capture, TryGetVideoSize={ok}, got {sourceVideoWidth}x{sourceVideoHeight}");
            }

            // Clear any stale LibVLC crop/scale only once (not on every zoom step,
            // to avoid triggering a vout reconfiguration that causes flicker).
            if (!vlcCropCleared)
            {
                mp.CropGeometry = string.Empty;
                mp.Scale = 0;
                vlcCropCleared = true;
            }

            // Get the current window client size (the visible area / clip rect).
            double viewWidth = ClientSize.Width;
            double viewHeight = ClientSize.Height;

            if (viewWidth <= 0 || viewHeight <= 0)
            {
                logger.Warn($"UpdateImageTransform: client size is {viewWidth}x{viewHeight}, cannot apply zoom {currentZoom:F2}.");
                return;
            }

            // Enlarge the VideoView by the zoom factor, centered on the window.
            // The Grid's ClipToBounds clips the excess, showing only the center.
            double scaledWidth = viewWidth * currentZoom;
            double scaledHeight = viewHeight * currentZoom;

            videoViewer.Width = scaledWidth;
            videoViewer.Height = scaledHeight;
            videoViewer.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            videoViewer.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;

            // Compute equivalent center crop for logging.
            if (sourceVideoWidth > 0 && sourceVideoHeight > 0)
            {
                int cropWidth = Math.Clamp((int)Math.Round(sourceVideoWidth / currentZoom), 1, sourceVideoWidth);
                int cropHeight = Math.Clamp((int)Math.Round(sourceVideoHeight / currentZoom), 1, sourceVideoHeight);
                int cropX = (sourceVideoWidth - cropWidth) / 2;
                int cropY = (sourceVideoHeight - cropHeight) / 2;

                logger.Info($"UpdateImageTransform: zoom={currentZoom:F2}, source={sourceVideoWidth}x{sourceVideoHeight}, " +
                            $"view={viewWidth:F0}x{viewHeight:F0}, VideoView resized to {scaledWidth:F0}x{scaledHeight:F0} (centered, clipped), " +
                            $"equiv. center crop={cropWidth}x{cropHeight}+{cropX}+{cropY}");
            }
            else
            {
                logger.Info($"UpdateImageTransform: zoom={currentZoom:F2}, view={viewWidth:F0}x{viewHeight:F0}, " +
                            $"VideoView resized to {scaledWidth:F0}x{scaledHeight:F0} (centered, clipped)");
            }
        }

        private void ApplyZoomDelta(double delta)
        {
            currentZoom = Math.Clamp(currentZoom + delta, MinZoom, MaxZoom);
        }

        private static bool TryGetVideoSize(LibVLCSharp.Shared.MediaPlayer mediaPlayer, out int width, out int height)
        {
            width = 0;
            height = 0;

            uint rawWidth = 0;
            uint rawHeight = 0;

            if (!mediaPlayer.Size(0, ref rawWidth, ref rawHeight))
            {
                return false;
            }

            if (rawWidth == 0 || rawHeight == 0)
            {
                return false;
            }

            width = (int)rawWidth;
            height = (int)rawHeight;
            return true;
        }

        private void UpdateWindowPosition()
        {
            if (svm.PinVideoWindowToMainWindow == false) return;

            Position = new Avalonia.PixelPoint(
                svm.MainWindowPosition.X + 1,
                svm.MainWindowPosition.Y
            );

            if (svm.DockInMainWindow)
            {
                Width = svm.MainWindowWidth - svm.SettingsWindowWidth / 2 + 9;
            }
            else
            {
                Width = svm.MainWindowWidth;
            }

            Height = svm.MainWindowHeight;
        }
    }
}
