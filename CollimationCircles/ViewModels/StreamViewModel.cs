using Avalonia.Threading;
using CollimationCircles.Helper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using LibVLCSharp.Shared;
using System;
using System.Diagnostics;
using System.Text;

namespace CollimationCircles.ViewModels
{
    public partial class StreamViewModel : BaseViewModel
    {
        private readonly IDialogService dialogService;
        private readonly LibVLC libVLC;
        private Process? proc;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
        private string mediaUri = "tcp/h264://192.168.1.129:8080";

        public MediaPlayer MediaPlayer { get; }

        [ObservableProperty]
        private string buttonTitle = string.Empty;

        [ObservableProperty]
        private string mediaPlayerLog = string.Empty;

        private readonly StringBuilder _mediaPlayerLog = new();

        private bool CanExecutePlayPause
        {
            get => !string.IsNullOrWhiteSpace(MediaUri);
        }

        public StreamViewModel(IDialogService dialogService)
        {
            this.dialogService = dialogService;

            if (OperatingSystem.IsLinux())
            {
                MediaUri = "tcp/h264://127.0.0.1:8080";
            }

            libVLC = new();
            //libVLC.Log += LibVLC_Log;

            MediaPlayer = new(libVLC);

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
                    Console.WriteLine("Starting video stream: libcamera-vid -t 0 --inline --nopreview --listen -o tcp://0.0.0.0:8080");

                    ProcessStartInfo startInfo = new()
                    {
                        FileName = "libcamera-vid",
                        Arguments = "-t 0 --inline --nopreview --listen -o tcp://0.0.0.0:8080"
                    };

                    proc = new()
                    {
                        StartInfo = startInfo
                    };

                    if (proc.Start())
                    {
                        MediaPlayerPlay();
                    }
                }
                else
                {
                    MediaPlayerPlay();
                }
            }
        }

        private void MediaPlayerPlay()
        {
            using var media = new Media(
                    libVLC,
                    MediaUri,
                    FromType.FromLocation
                    );

            MediaPlayer.Opening += MediaPlayer_Opening;
            MediaPlayer.Stopped += MediaPlayer_Stopped;

            MediaPlayer.Play(media);
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
            Dispatcher.UIThread.Post(() =>
            {
                dialogService?.Show<StreamViewModel>(null, this);
            });
        }

        private void CloseWebCamStream()
        {
            Dispatcher.UIThread.Post(() =>
            {
                dialogService?.Close(this);
                proc?.Close();
            });
        }
    }
}
