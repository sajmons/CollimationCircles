using Avalonia.Controls;
using Avalonia.Threading;
using CollimationCircles.Messages;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using System;

namespace CollimationCircles.Views
{
    public partial class StreamView : Window
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
                
        private readonly Grid? frameGrid;
        private readonly FrameRenderer? frameRenderer;
        private readonly SettingsViewModel svm;

        public StreamView()
        {
            InitializeComponent();
            svm = Ioc.Default.GetRequiredService<SettingsViewModel>();
            DataContext = Ioc.Default.GetRequiredService<CollimationAnalysisViewModel>();
                        
            frameGrid = this.Get<Grid>("FrameGrid");
            frameRenderer = this.Get<FrameRenderer>("FrameRenderer");

            WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) =>
            {
                UpdateWindowPosition();
            });

            WeakReferenceMessenger.Default.Register<CameraFrameMessage>(this, (r, m) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (frameGrid is not null) frameGrid.IsVisible = true;
                    frameRenderer?.SetFrame(m.Value);
                }, DispatcherPriority.Render);
            });

            Opened += WebCamStreamWindow_Opened;
            Closed += StreamView_Closed;
        }

        private void StreamView_Closed(object? sender, EventArgs e)
        {
            WeakReferenceMessenger.Default.UnregisterAll(this);

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
            frameRenderer?.InvalidateVisual();
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