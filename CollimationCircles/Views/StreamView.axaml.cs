using Avalonia.Controls;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CollimationCircles.Messages;
using CollimationCircles.Services.ScreenCapturing;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using System;
using Mat = OpenCvSharp.Mat;

namespace CollimationCircles.Views
{
    public partial class StreamView : Window, IScreenCaptureTarget
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
                
        private readonly Grid? frameGrid;
        private readonly Image? frameImage;
        private readonly Control? analysisOverlay;
        private readonly SettingsViewModel svm;

        private WriteableBitmap? bitmap;
        private PixelSize? frameSize;
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
                if (m.Display)
                {
                    QueueFrameForRender(m.Value.Clone());
                    return;
                }

                var size = new PixelSize(m.Value.Width, m.Value.Height);
                Dispatcher.UIThread.Post(() => ShowOverlayOnly(size), DispatcherPriority.Render);
            });

            Opened += WebCamStreamWindow_Opened;
            Closed += StreamView_Closed;
        }

        private void StreamView_Closed(object? sender, EventArgs e)
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);

            bitmap?.Dispose();
            bitmap = null;
            frameSize = null;
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
            if (analysisOverlay is null || frameGrid is null || frameSize is not { } size)
            {
                return;
            }

            var bounds = frameGrid.Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            var scale = Math.Min(bounds.Width / size.Width, bounds.Height / size.Height);
            analysisOverlay.Width = size.Width * scale;
            analysisOverlay.Height = size.Height * scale;
        }

        private void ShowOverlayOnly(PixelSize size)
        {
            lock (frameSync)
            {
                pendingFrame?.Dispose();
                pendingFrame = null;
            }

            // The window is transparent so the screen content behind it stays visible.
            Background = Brushes.Transparent;

            if (frameImage is not null)
            {
                frameImage.IsVisible = false;
                frameImage.Source = null;
            }

            if (frameGrid is not null)
            {
                frameGrid.Background = Brushes.Transparent;
                frameGrid.IsVisible = true;
            }

            bitmap?.Dispose();
            bitmap = null;

            if (frameSize != size)
            {
                frameSize = size;
                UpdateOverlaySize();
            }
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

            ClearValue(BackgroundProperty);

            if (frameImage is not null)
            {
                frameImage.IsVisible = true;
            }

            if (frameGrid is not null)
            {
                frameGrid.Background = Brushes.Black;
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
            }

            if (frameSize != bitmap.PixelSize)
            {
                frameSize = bitmap.PixelSize;
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

        public PixelRect GetCaptureRect()
        {
            var root = this.Get<Grid>("RootGrid");
            var origin = root.PointToScreen(new Point(0, 0));
            var scale = RenderScaling;

            return new PixelRect(
                origin,
                new PixelSize((int)Math.Round(root.Bounds.Width * scale), (int)Math.Round(root.Bounds.Height * scale)));
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