using Avalonia.Controls;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CollimationCircles.Messages;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Runtime.InteropServices;

namespace CollimationCircles.Views
{
    public partial class StreamView : Window
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
                
        private readonly Grid? frameGrid;
        private readonly Image? frameImage;
        private readonly SettingsViewModel svm;

        private WriteableBitmap? bitmap;
        private byte[]? bgraBuffer;
        private CameraFrame? pendingFrame;
        private bool renderQueued;

        public StreamView()
        {
            InitializeComponent();
            svm = Ioc.Default.GetRequiredService<SettingsViewModel>();
            DataContext = Ioc.Default.GetRequiredService<CollimationAnalysisViewModel>();
                        
            frameGrid = this.Get<Grid>("FrameGrid");
            frameImage = this.Get<Image>("FrameImage");

            WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) =>
            {
                UpdateWindowPosition();
            });

            WeakReferenceMessenger.Default.Register<CameraFrameMessage>(this, (r, m) =>
            {
                QueueFrameForRender(m.Value);
            });

            Opened += WebCamStreamWindow_Opened;
            Closed += StreamView_Closed;
        }

        private void StreamView_Closed(object? sender, EventArgs e)
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);

            bitmap?.Dispose();
            bitmap = null;
            bgraBuffer = null;
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
        }

        private void QueueFrameForRender(CameraFrame frame)
        {
            pendingFrame = frame;
            if (renderQueued)
            {
                return;
            }

            renderQueued = true;
            Dispatcher.UIThread.Post(RenderPendingFrame, DispatcherPriority.Render);
        }

        private void RenderPendingFrame()
        {
            renderQueued = false;
            var frame = pendingFrame;
            pendingFrame = null;
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
                bgraBuffer = new byte[frame.Width * frame.Height * 4];

                if (frameImage is not null)
                {
                    frameImage.Source = bitmap;
                }
            }

            var bgra = bgraBuffer ??= new byte[frame.Width * frame.Height * 4];
            ExpandGrayToBgra(frame.Pixels, bgra);

            using var fb = bitmap.Lock();
            var copyBytesPerRow = frame.Width * 4;
            if (fb.RowBytes == copyBytesPerRow)
            {
                Marshal.Copy(bgra, 0, fb.Address, bgra.Length);
                return;
            }

            for (int y = 0; y < frame.Height; y++)
            {
                Marshal.Copy(bgra, y * copyBytesPerRow, fb.Address + y * fb.RowBytes, copyBytesPerRow);
            }
        }

        private static void ExpandGrayToBgra(byte[] gray, byte[] bgra)
        {
            int src = 0;
            int dst = 0;
            while (src < gray.Length)
            {
                var value = gray[src++];
                bgra[dst++] = value;
                bgra[dst++] = value;
                bgra[dst++] = value;
                bgra[dst++] = 255;
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