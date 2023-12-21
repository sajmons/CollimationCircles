using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
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
        const string defaultProtocol = "tcp/h264";
        const string defaultLocalAddress = "127.0.0.1";
        const string defaultRemoteAddress = "192.168.1.174";
        const string defaultPort = "8080";

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly IDialogService dialogService;
        private readonly LibVLC libVLC;
        private Process? proc;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
        private string fullAddress;

        private string protocol = defaultProtocol;

        private string address;

        private string port = defaultPort;

        private string pathAndQuery;        

        public MediaPlayer MediaPlayer { get; }

        [ObservableProperty]
        private string buttonTitle = string.Empty;

        public bool CanExecutePlayPause
        {
            get => !string.IsNullOrWhiteSpace(protocol) && !string.IsNullOrWhiteSpace(address) && !string.IsNullOrWhiteSpace(port);
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEnabled))]
        private bool isPlaying = false;

        public bool IsEnabled => !IsPlaying;

        private readonly SettingsViewModel settingsViewModel;

        [ObservableProperty]
        private bool pinVideoWindowToMainWindow = true;

        private static bool LocalConnectionPossible => OperatingSystem.IsLinux();

        public StreamViewModel(IDialogService dialogService, SettingsViewModel settingsViewModel)
        {
            this.dialogService = dialogService;
            this.settingsViewModel = settingsViewModel;
            address = LocalConnectionPossible ? defaultLocalAddress : defaultRemoteAddress;
            pathAndQuery = string.Empty;

            FullAddress = GetFullUrlFromParts();

            // https://wiki.videolan.org/VLC_command-line_help/

            string[] libVLCOptions = [
                //$"--width=320",
                //$"--height=240",
                //$"--zoom=1.5",
                //$"--log-verbose=0"
            ];

            libVLC = new(libVLCOptions);
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
            if (!string.IsNullOrWhiteSpace(protocol) && !string.IsNullOrWhiteSpace(address) && !string.IsNullOrWhiteSpace(port))
            {
                if (LocalConnectionPossible)
                {
                    try
                    {
                        ProcessStartInfo startInfo = new()
                        {
                            // libcamera-vid -t 0 --inline --nopreview --listen -o tcp://0.0.0.0:8080
                            FileName = "libcamera-vid",
                            Arguments = $"-t 0 --inline --nopreview --listen -o tcp://0.0.0.0:{port}"
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
            string mrl = $"{protocol}://{address}:{port}";

            string[] mediaAdditionalOptions = [
                //$"--osd",
                //$"--video-title=my title",
                //$"--avcodec-hw=any",
                //$"--zoom=0.25"
            ];

            using var media = new Media(
                    libVLC,
                    mrl,
                    FromType.FromLocation,
                    mediaAdditionalOptions
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

            //MediaPlayer.SetAdjustFloat(VideoAdjustOption.Enable, 1);
            //MediaPlayer.SetAdjustInt(VideoAdjustOption.Enable, 1);
            //MediaPlayer.SetAdjustFloat(VideoAdjustOption.Gamma, 0.1f);
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

        private string GetFullUrlFromParts()
        {
            string newRemoteAddress = address ?? defaultRemoteAddress;
            string addr = LocalConnectionPossible ? defaultLocalAddress : newRemoteAddress;
            string pth = string.IsNullOrWhiteSpace(pathAndQuery) ? "" : pathAndQuery;

            return $"{protocol}://{addr}:{port}{pth}";
        }

        partial void OnPinVideoWindowToMainWindowChanged(bool oldValue, bool newValue)
        {
            settingsViewModel.PinVideoWindowToMainWindow = newValue;
        }

        partial void OnFullAddressChanged(string? oldValue, string newValue)
        {
            string mrlRegExpr = "^(.*):\\/\\/(.*):([0-9]+)(\\/.*?.*)*";

            Match m = Regex.Match(FullAddress, mrlRegExpr);

            if (m.Success)
            {
                logger.Info($"Media URL parsed sucessfully '{FullAddress}'");
                protocol = m.Groups[1].Value;
                address = m.Groups[2].Value;
                port = m.Groups[3].Value;
                pathAndQuery = m.Groups[4].Value;                
            }
            else
            {
                logger.Warn($"Please check media URL for errors '{FullAddress}'");
                protocol = string.Empty;
                address = string.Empty;
                port = string.Empty;
                pathAndQuery = string.Empty;
            }
        }
    }
}
