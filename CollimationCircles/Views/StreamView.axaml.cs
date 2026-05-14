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
        private const double MinZoom = 0.5;
        private const double MaxZoom = 4.0;
        private const double ZoomStep = 0.2;

        private readonly VideoView videoViewer;
        private readonly SettingsViewModel svm;
        private double currentZoom = 1.0;
        private int sourceVideoWidth;
        private int sourceVideoHeight;
        private bool sourceDimensionsCaptured = false;

        public StreamView()
        {
            InitializeComponent();

            svm = Ioc.Default.GetRequiredService<SettingsViewModel>();

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
                    
                    // Capture source dimensions once when stream starts playing
                    mp.Playing += (s, ev) =>
                    {
                        if (!sourceDimensionsCaptured)
                        {
                            if (TryGetVideoSize(mp, out int w, out int h))
                            {
                                sourceVideoWidth = w;
                                sourceVideoHeight = h;
                                sourceDimensionsCaptured = true;
                            }
                        }
                    };
                }

                currentZoom = 1.0;
                sourceVideoWidth = 0;
                sourceVideoHeight = 0;
                sourceDimensionsCaptured = false;
                UpdateImageTransform();
                UpdateWindowPosition();
            }
        }

        private void ApplyZoom(ImageZoomAction action)
        {
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

            UpdateImageTransform();
        }

        private void UpdateImageTransform()
        {
            var mp = videoViewer.MediaPlayer;

            if (mp is null)
            {
                return;
            }

            // Use LibVLC crop geometry because native video surfaces may ignore Avalonia RenderTransform.
            if (currentZoom <= 1.0)
            {
                mp.CropGeometry = string.Empty;
                mp.Scale = 0;
                return;
            }

            // Try to capture source dimensions on first zoom if not yet captured
            if (!sourceDimensionsCaptured && (sourceVideoWidth <= 0 || sourceVideoHeight <= 0))
            {
                TryGetVideoSize(mp, out sourceVideoWidth, out sourceVideoHeight);
                sourceDimensionsCaptured = true;  // Mark as attempted even if TryGetVideoSize fails
            }

            // If we still don't have source dimensions, can't apply zoom
            if (sourceVideoWidth <= 0 || sourceVideoHeight <= 0)
            {
                return;
            }

            int sourceWidth = sourceVideoWidth;
            int sourceHeight = sourceVideoHeight;

            int cropWidth = Math.Clamp((int)Math.Round(sourceWidth / currentZoom), 1, sourceWidth);
            int cropHeight = Math.Clamp((int)Math.Round(sourceHeight / currentZoom), 1, sourceHeight);

            double centerX = sourceWidth / 2.0;
            double centerY = sourceHeight / 2.0;

            double halfCropWidth = cropWidth / 2.0;
            double halfCropHeight = cropHeight / 2.0;

            centerX = Math.Clamp(centerX, halfCropWidth, sourceWidth - halfCropWidth);
            centerY = Math.Clamp(centerY, halfCropHeight, sourceHeight - halfCropHeight);

            int cropX = Math.Clamp((int)Math.Round(centerX - halfCropWidth), 0, sourceWidth - cropWidth);
            int cropY = Math.Clamp((int)Math.Round(centerY - halfCropHeight), 0, sourceHeight - cropHeight);

            string crop = $"{cropWidth}x{cropHeight}+{cropX}+{cropY}";
            mp.CropGeometry = crop;
            mp.Scale = 0;
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
