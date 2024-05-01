using Avalonia.Controls;
using CollimationCircles.Messages;
using CollimationCircles.Services;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using LibVLCSharp.Avalonia;

namespace CollimationCircles.Views
{
    public partial class StreamView : Window
    {
        private readonly VideoView videoViewer;
        private readonly SettingsViewModel svm;

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

            Opened += WebCamStreamWindow_Opened;
        }

        private void WebCamStreamWindow_Opened(object? sender, System.EventArgs e)
        {
            var mp = Ioc.Default.GetRequiredService<ILibVLCService>().MediaPlayer;

            if (videoViewer != null)
            {
                videoViewer.MediaPlayer = mp;

                UpdateWindowPosition();
            }
        }

        private void UpdateWindowPosition()
        {
            if (svm.PinVideoWindowToMainWindow == false) return;

            Position = svm.MainWindowPosition;

            if (svm.DockInMainWindow)
            {
                Width = svm.MainWindowWidth - svm.SettingsWindowWidth / 2;
            }
            else
            {
                Width = svm.MainWindowWidth;
            }

            Height = svm.MainWindowHeight;
        }
    }
}
