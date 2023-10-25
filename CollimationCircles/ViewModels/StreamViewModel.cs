using Avalonia.Threading;
using CollimationCircles.Helper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using LibVLCSharp.Shared;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace CollimationCircles.ViewModels
{
    public partial class StreamViewModel : BaseViewModel, IViewClosed
    {
        private readonly IDialogService dialogService;
        private LibVLC? libVLC;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
        private string mediaUri = "tcp/h264://192.168.1.174:8080";

        [ObservableProperty]
        private MediaPlayer? mediaPlayer;

        [ObservableProperty]
        private string buttonTitle = string.Empty;

        [ObservableProperty]
        private string mediaPlayerLog = string.Empty;

        private readonly StringBuilder _mediaPlayerLog = new();

        private bool CanExecutePlayPause
        {
            get => !string.IsNullOrWhiteSpace(MediaUri);
        }

        [ObservableProperty]
        private INotifyPropertyChanged? webCamStreamDialogViewModel;

        public StreamViewModel(IDialogService dialogService)
        {
            this.dialogService = dialogService;
            Initialize();
        }

        public void Initialize()
        {
            if (!Avalonia.Controls.Design.IsDesignMode)
            {
                libVLC = new LibVLC(enableDebugLogs: false);
                libVLC.Log += LibVLC_Log;

                MediaPlayer = new MediaPlayer(libVLC) { };
            }

            ButtonTitle = DynRes.TryGetString("Start");
        }

        private void LibVLC_Log(object? sender, LogEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _mediaPlayerLog.AppendLine(e.Message);
                MediaPlayerLog = _mediaPlayerLog.ToString();
            });
        }

        private void Play()
        {
            if (!string.IsNullOrWhiteSpace(MediaUri))
            {
                if (OperatingSystem.IsLinux())
                {
                    // libcamera-vid -t 0 --inline --nopreview --listen -o tcp://0.0.0.0:8080
                    
                    ProcessStartInfo startInfo = new ProcessStartInfo() {
                        FileName = "libcamera-vid",
                        Arguments = "-t 0 --inline --nopreview --listen -o tcp://0.0.0.0:8080"
                    };
                    
                    Process proc = new() {
                        StartInfo = startInfo
                    };

                    proc.Start();
                }

                if (libVLC != null && MediaPlayer != null)
                {
                    string[] Media_AdditionalOptions = Array.Empty<string>();

                    using var media = new Media(
                        libVLC,
                        MediaUri,
                        FromType.FromLocation,
                        Media_AdditionalOptions
                        );

                    MediaPlayer.Opening += MediaPlayer_Opening;
                    MediaPlayer.Stopped += MediaPlayer_Stopped;

                    MediaPlayer.Play(media);
                    media.Dispose();
                }
            }
        }

        private void MediaPlayer_Stopped(object? sender, EventArgs e)
        {
            CloseWebCamStream();
            ButtonTitle = DynRes.TryGetString("Start");
        }

        private void MediaPlayer_Opening(object? sender, EventArgs e)
        {
            ShowWebCamStream();
            ButtonTitle = DynRes.TryGetString("Stop");
            _mediaPlayerLog.Clear();
            MediaPlayerLog = _mediaPlayerLog.ToString();
        }

        [RelayCommand(CanExecute = nameof(CanExecutePlayPause))]
        private void PlayPause()
        {
            if (MediaPlayer != null)
            {
                if (!MediaPlayer.IsPlaying)
                {
                    Play();
                }
                else
                {
                    MediaPlayer.Stop();
                }
            }
        }

        private void ShowWebCamStream()
        {
            if (WebCamStreamDialogViewModel is null)
            {
                WebCamStreamDialogViewModel = dialogService?.CreateViewModel<StreamViewModel>();

                if (WebCamStreamDialogViewModel is not null)
                {
                    dialogService?.Show<StreamViewModel>(null, WebCamStreamDialogViewModel);
                }
            }
        }

        private void CloseWebCamStream()
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (WebCamStreamDialogViewModel is not null)
                {
                    dialogService?.Close(WebCamStreamDialogViewModel);
                }
            });
        }

        public void OnClosed()
        {
            WebCamStreamDialogViewModel = null;
        }
    }
}
