using System;
using System.Diagnostics;
using Avalonia.Threading;
using CollimationCircles.Helper;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using LibVLCSharp.Shared;

namespace CollimationCircles.ViewModels
{
    public partial class StreamViewModel : BaseViewModel
    {
        const string defaultLocalAddress = "127.0.0.1";
        const string defaultRemoteAddress = "192.168.1.129";
        const string defaultPort = "8080";

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly IDialogService dialogService;
        private readonly LibVLC libVLC;
        private Process? proc;
        
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
        private string address;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
        private string port = defaultPort;

        public MediaPlayer MediaPlayer { get; }

        [ObservableProperty]
        private string buttonTitle = string.Empty;

        [ObservableProperty]
        private bool isRemoteConnection = !OperatingSystem.IsLinux();

        public bool CanExecutePlayPause
        {
            get => !string.IsNullOrWhiteSpace(Address) && !string.IsNullOrWhiteSpace(Port);
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEnabled))]
        private bool isPlaying = false;

        public bool IsEnabled => !IsPlaying && IsRemoteConnection;        

        public StreamViewModel(IDialogService dialogService)
        {
            this.dialogService = dialogService;

            Address = GetUrl();

            libVLC = new();
            //libVLC.Log += LibVLC_Log;

            MediaPlayer = new(libVLC);
            MediaPlayer.Opening += MediaPlayer_Opening;
            MediaPlayer.Playing += MediaPlayer_Playing;
            MediaPlayer.Stopped += MediaPlayer_Stopped;                     

            ButtonTitle = DynRes.TryGetString("Start");            
        }

        private void LibVLC_Log(object? sender, LogEventArgs e)
        {
            logger.Info(e.Message);
        }

        private void Play()
        {
            if (!string.IsNullOrWhiteSpace(Address))
            {
                if (!IsRemoteConnection)
                {
                    try
                    {
                        ProcessStartInfo startInfo = new()
                        {
                            FileName = "libcamera-vid",
                            Arguments = $"-t 0 --inline --nopreview --listen -o tcp://0.0.0.0:{Port}"
                        };

                        proc = new()
                        {
                            StartInfo = startInfo
                        };

                        if (proc.Start())
                        {
                            logger.Info($"Started LibCamera process '{startInfo.FileName} {startInfo.Arguments}'");
                            MediaPlayerPlay();
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Info("Local connection is only supported on Linux.\nPlease use 'Connect remote' option.");
                        logger.Error(ex.Message);
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
            string mrl = $"tcp/h264://{Address}:{Port}";

            using var media = new Media(
                    libVLC,
                    mrl,
                    FromType.FromLocation
                    );

            MediaPlayer.Play(media);
            logger.Info($"Playing web camera stream: '{media.Mrl}'");
        }

        private void MediaPlayer_Opening(object? sender, EventArgs e)
        {
            logger.Info($"MediaPlayer opening");

            ShowWebCamStream();
            ButtonTitle = DynRes.TryGetString("Stop");
            IsPlaying = MediaPlayer.IsPlaying;
        }

        private void MediaPlayer_Playing(object? sender, EventArgs e)
        {
            logger.Info($"MediaPlayer playing");
            IsPlaying = MediaPlayer.IsPlaying;
        }

        private void MediaPlayer_Stopped(object? sender, EventArgs e)
        {
            logger.Info($"MediaPlayer stopped");

            CloseWebCamStream();
            ButtonTitle = DynRes.TryGetString("Start");
            IsPlaying = MediaPlayer.IsPlaying;
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
                logger.Info($"Opened web camera stream window");
            });
        }

        private void CloseWebCamStream()
        {
            Dispatcher.UIThread.Post(() =>
            {
                dialogService?.Close(this);
                logger.Info($"Closed web camera stream window");

                proc?.Close();
                logger.Info($"Closed LibCamera process");
            });
        }

        partial void OnIsRemoteConnectionChanged(bool oldValue, bool newValue)
        {
            string oldMediaUri = Address;

            if (newValue) Address = defaultRemoteAddress;

            Address = GetUrl();
            Debug.WriteLine($"MediaUrl changed from '{oldMediaUri}:{Port}' to '{Address}:{Port}'");
        }

        private string GetUrl()
        {
            string newRemoteAddress = Address ?? defaultRemoteAddress;

            return IsRemoteConnection ? newRemoteAddress : defaultLocalAddress;
        }        
    }
}
