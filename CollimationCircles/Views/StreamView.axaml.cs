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

        private readonly VideoView? videoViewer;
        private readonly Grid? frameGrid;
        private readonly FrameRenderer? frameRenderer;
        private readonly SettingsViewModel svm;
        private double currentZoom = 1.0;
        private int sourceVideoWidth;
        private int sourceVideoHeight;
        private bool sourceDimensionsCaptured = false;

        private IZwoFrameSource? _zwoFrameSource;
        private bool _usingZwoDirect;

        public StreamView()
        {
            InitializeComponent();
            svm = Ioc.Default.GetRequiredService<SettingsViewModel>();
            DataContext = Ioc.Default.GetRequiredService<CollimationAnalysisViewModel>();

            videoViewer = this.Get<VideoView>("VideoViewer");
            frameGrid = this.Get<Grid>("FrameGrid");
            frameRenderer = this.Get<FrameRenderer>("FrameRenderer");

            WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) =>
            {
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

            if (_zwoFrameSource is not null)
            {
                _zwoFrameSource.FrameReady -= OnZwoFrameReady;
                _zwoFrameSource = null;
            }

            if (frameGrid is not null)
                frameGrid.SizeChanged -= OnFrameGridSizeChanged;
        }

        private void WebCamStreamWindow_Opened(object? sender, System.EventArgs e)
        {
            _zwoFrameSource = Ioc.Default.GetRequiredService<IZwoFrameSource>();
            _usingZwoDirect = _zwoFrameSource.IsStreaming;

            if (_usingZwoDirect)
            {
                // ZWO direct-rendering path: show FrameRenderer, hide VideoView.
                if (videoViewer is not null) videoViewer.IsVisible = false;
                if (frameGrid is not null) frameGrid.IsVisible = true;

                sourceVideoWidth = _zwoFrameSource.FrameWidth;
                sourceVideoHeight = _zwoFrameSource.FrameHeight;
                sourceDimensionsCaptured = sourceVideoWidth > 0 && sourceVideoHeight > 0;

                _zwoFrameSource.FrameReady += OnZwoFrameReady;

                if (frameGrid is not null)
                    frameGrid.SizeChanged += OnFrameGridSizeChanged;

                currentZoom = 1.0;
                UpdateWindowPosition();
            }
            else
            {
                // LibVLC path: show VideoView, hide FrameRenderer.
                if (videoViewer is not null) videoViewer.IsVisible = true;
                if (frameGrid is not null) frameGrid.IsVisible = false;

                var mp = Ioc.Default.GetRequiredService<ILibVLCService>().MediaPlayer;

                if (videoViewer != null && mp is not null)
                {
                    videoViewer.MediaPlayer = mp;

                    mp.Playing += (s, ev) =>
                    {
                        if (!sourceDimensionsCaptured && TryGetVideoSize(mp, out int w, out int h))
                        {
                            sourceVideoWidth = w;
                            sourceVideoHeight = h;
                            sourceDimensionsCaptured = true;
                        }

                        if (currentZoom <= 1.0)
                        {
                            mp.CropGeometry = string.Empty;
                            mp.Scale = 0;
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

        private void OnFrameGridSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            frameRenderer?.InvalidateVisual();
        }

        private void OnZwoFrameReady(byte[] frame, int width, int height)
        {
            frameRenderer?.SetJpegFrame(frame);
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
            if (_usingZwoDirect)
            {
                if (frameRenderer is not null)
                    frameRenderer.Zoom = currentZoom;
                return;
            }

            var mp = videoViewer?.MediaPlayer;
            if (mp is null)
                return;

            if (currentZoom <= 1.0)
            {
                mp.CropGeometry = string.Empty;
                mp.Scale = 0;
                return;
            }

            if (!sourceDimensionsCaptured && (sourceVideoWidth <= 0 || sourceVideoHeight <= 0))
            {
                TryGetVideoSize(mp, out sourceVideoWidth, out sourceVideoHeight);
                sourceDimensionsCaptured = true;
            }

            if (sourceVideoWidth <= 0 || sourceVideoHeight <= 0)
                return;

            int cropWidth = Math.Clamp((int)Math.Round(sourceVideoWidth / currentZoom), 1, sourceVideoWidth);
            int cropHeight = Math.Clamp((int)Math.Round(sourceVideoHeight / currentZoom), 1, sourceVideoHeight);

            double centerX = sourceVideoWidth / 2.0;
            double centerY = sourceVideoHeight / 2.0;
            double halfCropWidth = cropWidth / 2.0;
            double halfCropHeight = cropHeight / 2.0;

            centerX = Math.Clamp(centerX, halfCropWidth, sourceVideoWidth - halfCropWidth);
            centerY = Math.Clamp(centerY, halfCropHeight, sourceVideoHeight - halfCropHeight);

            int cropX = Math.Clamp((int)Math.Round(centerX - halfCropWidth), 0, sourceVideoWidth - cropWidth);
            int cropY = Math.Clamp((int)Math.Round(centerY - halfCropHeight), 0, sourceVideoHeight - cropHeight);

            mp.CropGeometry = $"{cropWidth}x{cropHeight}+{cropX}+{cropY}";
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
                return false;

            if (rawWidth == 0 || rawHeight == 0)
                return false;

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