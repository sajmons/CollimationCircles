using Avalonia.Controls;
using CollimationCircles.Messages;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using LibVLCSharp.Avalonia;

namespace CollimationCircles.Views
{
    public partial class StreamView : Window
    {
        private readonly VideoView videoViewer;
        private readonly StreamViewModel? vm;
        private readonly SettingsViewModel? svm;

        public StreamView()
        {
            InitializeComponent();

            vm = Ioc.Default.GetService<StreamViewModel>();
            svm = Ioc.Default.GetService<SettingsViewModel>();

            videoViewer = this.Get<VideoView>("VideoViewer");

            WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) =>
            {
                UpdateWindowPosition();
            });

            Opened += WebCamStreamWindow_Opened;
        }

        private void WebCamStreamWindow_Opened(object? sender, System.EventArgs e)
        {
            if (videoViewer != null && vm!.MediaPlayer != null)
            {
                videoViewer.MediaPlayer = vm?.MediaPlayer;

                UpdateWindowPosition();
            }
        }

        private void UpdateWindowPosition()
        {
            if (svm?.PinVideoWindowToMainWindow == false) return;

            Position = svm!.MainWindowPosition;

            if (svm!.DockInMainWindow)
            {
                Width = svm!.MainWindowWidth - svm!.SettingsWindowWidth / 2;
            }
            else
            {
                Width = svm!.MainWindowWidth;
            }

            Height = svm!.MainWindowHeight;
        }
    }
}
