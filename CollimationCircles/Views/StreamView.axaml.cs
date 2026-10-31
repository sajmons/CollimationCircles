using Avalonia.Controls;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CollimationCircles.Messages;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using System;
using Mat = OpenCvSharp.Mat;

namespace CollimationCircles.Views
{
    public partial class StreamView : Window
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
                
        private readonly Grid? frameGrid;
        private readonly Image? frameImage;
        private readonly Control? analysisOverlay;
        private readonly SettingsViewModel svm;

        private WriteableBitmap? bitmap;
        private Mat? pendingFrame;
        private bool renderQueued;
        private readonly object frameSync = new();

        public StreamView()
        {
            InitializeComponent();
            svm = Ioc.Default.GetRequiredService<SettingsViewModel>();
            DataContext = Ioc.Default.GetRequiredService<CollimationAnalysisViewModel>();
                        
            frameGrid = this.Get<Grid>("FrameGrid");
            frameImage = this.Get<Image>("FrameImage");
            analysisOverlay = this.Get<Control>("AnalysisOverlay");
            frameGrid.SizeChanged += OnFrameGridSizeChanged;

            WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) =>
            {
                UpdateWindowPosition();
            });

            WeakReferenceMessenger.Default.Register<MatMessage>(this, (r, m) =>
            {
                QueueFrameForRender(m.Value.Clone());
            });

            Opened += WebCamStreamWindow_Opened;
            Closed += StreamView_Closed;
        }

        private void StreamView_Closed(object? sender, EventArgs e)
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);

            bitmap?.Dispose();
            bitmap = null;
            if (frameImage is not null)
            {
                frameImage.Source = null;
            }

            if (frameGrid is not null)
                frameGrid.SizeChanged -= OnFrameGridSizeChanged;
        }

        private void WebCamStreamWindow_Opened(object? sender, System.EventArgs e)
        {
            {
                if (frameGrid is not null) frameGrid.IsVisible = false;

                UpdateWindowPosition();
            }
        }

        private void OnFrameGridSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            UpdateOverlaySize();
        }

        private void UpdateOverlaySize()
        {
            if (analysisOverlay is null || frameGrid is null || bitmap is null)
            {
                return;
            }

            var bounds = frameGrid.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            var scale = Math.Min(bounds.Width / bitmap.PixelSize.Width, bounds.Height / bitmap.PixelSize.Height);
            analysisOverlay.Width = bitmap.PixelSize.Width * scale;
            analysisOverlay.Height = bitmap.PixelSize.Height * scale;
        }

        private void QueueFrameForRender(Mat frame)
        {
            lock (frameSync)
            {
                pendingFrame?.Dispose();
                pendingFrame = frame;
                if (renderQueued)
                {
                    return;
                }

                renderQueued = true;
            }

            Dispatcher.UIThread.Post(RenderPendingFrame, DispatcherPriority.Render);
        }

        private void RenderPendingFrame()
        {
            Mat? frame;
            lock (frameSync)
            {
                renderQueued = false;
                frame = pendingFrame;
                pendingFrame = null;
            }

            if (frame is null)
            {
                return;
            }

            if (frameGrid is not null)
            {
                frameGrid.IsVisible = true;
            }

            if (bitmap is null || bitmap.PixelSize.Width != frame.Width || bitmap.PixelSize.Height != frame.Height)
            {
                bitmap?.Dispose();
                bitmap = new WriteableBitmap(
                    new PixelSize(frame.Width, frame.Height),
                    new Vector(96, 96),
                    Avalonia.Platform.PixelFormat.Bgra8888,
                    Avalonia.Platform.AlphaFormat.Opaque);

                if (frameImage is not null)
                {
                    frameImage.Source = bitmap;
                }

                UpdateOverlaySize();
            }

            using (frame)
            {
                var code = frame.Channels() switch
                {
                    1 => OpenCvSharp.ColorConversionCodes.GRAY2BGRA,
                    3 => OpenCvSharp.ColorConversionCodes.BGR2BGRA,
                    _ => (OpenCvSharp.ColorConversionCodes?)null
                };

                if (code is null && frame.Channels() != 4)
                {
                    logger.Warn($"Unsupported frame channel count: {frame.Channels()}");
                    return;
                }

                using var fb = bitmap.Lock();
                using var target = Mat.FromPixelData(frame.Height, frame.Width, OpenCvSharp.MatType.CV_8UC4, fb.Address, fb.RowBytes);

                if (code is { } c)
                {
                    OpenCvSharp.Cv2.CvtColor(frame, target, c);
                }
                else
                {
                    frame.CopyTo(target);
                }

                frameImage?.InvalidateVisual();
            }
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