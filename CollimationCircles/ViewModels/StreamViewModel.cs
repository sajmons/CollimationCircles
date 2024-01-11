using System;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using CollimationCircles.Helper;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using LibVLCSharp.Shared;

namespace CollimationCircles.ViewModels
{
    public partial class StreamViewModel : BaseViewModel
    {
        const string defaultProtocol = "tcp/h264";
        const string defaultLocalAddress = "0.0.0.0";
        const string defaultRemoteAddress = "192.168.1.174";
        const string defaultPort = "8080";

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly IDialogService dialogService;
        private readonly LibVLC libVLC;

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
            get => !string.IsNullOrWhiteSpace(protocol) && !string.IsNullOrWhiteSpace(address);
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEnabled))]
        private bool isPlaying = false;

        public bool IsEnabled => !IsPlaying;

        private readonly SettingsViewModel settingsViewModel;

        [ObservableProperty]
        private bool pinVideoWindowToMainWindow = true;

        [ObservableProperty]
        private bool localConnectionPossible = false;

        [ObservableProperty]
        private int cameraStreamTimeout = 1000;

        public StreamViewModel(IDialogService dialogService, SettingsViewModel settingsViewModel)
        {
            this.dialogService = dialogService;
            this.settingsViewModel = settingsViewModel;

            PinVideoWindowToMainWindow = settingsViewModel.PinVideoWindowToMainWindow;
            CameraStreamTimeout = settingsViewModel.CameraStreamTimeout;

            LocalConnectionPossible = AppService.IsPackageInstalled(AppService.LIBCAMERA_APPS).GetAwaiter().GetResult();

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
            logger.Debug(e.Message);
        }

        private void Play()
        {
            if (!string.IsNullOrWhiteSpace(protocol) && !string.IsNullOrWhiteSpace(address))
            {
                if (LocalConnectionPossible)
                {
                    logger.Info($"Just in case kill '{AppService.LIBCAMERA_VID}' process");
                    AppService.ExecuteCommand("pkill", [AppService.LIBCAMERA_VID]);

                    // proc = await AppService.StartTCPCameraStream($"0.0.0.0:{port}");
                    logger.Info("Starting camera video stream");
                    AppService.ExecuteCommand(
                        AppService.LIBCAMERA_VID,
                        [$"-t", "0", "--inline", "--nopreview", "--listen", "-o", $"tcp://{defaultLocalAddress}:{port}"], () =>
                        {
                            
                        }, CameraStreamTimeout);
                    logger.Info("Camera video stream started");
                }
                
                MediaPlayerPlay();                
            }
        }

        private void MediaPlayerPlay()
        {
            string mrl = GetFullUrlFromParts();

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
            logger.Trace($"MediaPlayer opening");

            ShowWebCamStream();
            ButtonTitle = DynRes.TryGetString("Stop");
            IsPlaying = MediaPlayer.IsPlaying;
        }

        private void MediaPlayer_Playing(object? sender, EventArgs e)
        {
            logger.Trace($"MediaPlayer playing");
            IsPlaying = MediaPlayer.IsPlaying;

            //MediaPlayer.SetAdjustFloat(VideoAdjustOption.Enable, 1);
            //MediaPlayer.SetAdjustInt(VideoAdjustOption.Enable, 1);
            //MediaPlayer.SetAdjustFloat(VideoAdjustOption.Gamma, 0.1f);
        }

        private void MediaPlayer_Stopped(object? sender, EventArgs e)
        {
            logger.Trace($"MediaPlayer stopped");

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
                logger.Trace($"Opened web camera stream window");
            });
        }

        private void CloseWebCamStream()
        {
            Dispatcher.UIThread.Post(() =>
            {
                dialogService?.Close(this);
                logger.Trace($"Closed web camera stream window");
            });
        }

        private string GetFullUrlFromParts()
        {
            string newRemoteAddress = address ?? defaultRemoteAddress;
            string addr = LocalConnectionPossible ? defaultLocalAddress : newRemoteAddress;
            string pth = string.IsNullOrWhiteSpace(pathAndQuery) ? "" : pathAndQuery;
            string prt = string.IsNullOrWhiteSpace(port) ? "" : $":{port}";

            return $"{protocol}://{addr}{prt}{pth}";
        }

        partial void OnPinVideoWindowToMainWindowChanged(bool oldValue, bool newValue)
        {
            settingsViewModel.PinVideoWindowToMainWindow = newValue;
        }

        partial void OnCameraStreamTimeoutChanged(int oldValue, int newValue)
        {            
            settingsViewModel.CameraStreamTimeout = newValue;
        }

        partial void OnFullAddressChanged(string? oldValue, string newValue)
        {
            string url = @"^(?:(?<protocol>tcp\/h264|http|https):\/\/)?(?<host>[\w\.-]+)(?::(?<port>\d+))?(?<path>\/\S*)?$";

            Match m = Regex.Match(FullAddress, url);

            if (m.Success)
            {
                logger.Debug($"Media URL parsed sucessfully '{FullAddress}'");
                protocol = m.Groups["protocol"].Value;
                address = m.Groups["host"].Value;

                string p = m.Groups["port"].Value;

                port = string.IsNullOrWhiteSpace(p) ? string.Empty : $"{p}";
                pathAndQuery = m.Groups["path"].Value;
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
