using Avalonia.Threading;
using CollimationCircles.Helper;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using LibVLCSharp.Shared;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace CollimationCircles.ViewModels
{
    public partial class StreamViewModel : BaseViewModel
    {
        const string tcpH264Protocol = "tcp/h264";
        const string defaultProtocol = "http";
        const string defaultLocalAddress = "0.0.0.0";
        const string defaultPort = "8080";        

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly IDialogService dialogService;
        private readonly IVideoStreamService videoStreamService;
        private readonly LibVLC libVLC;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
        [RegularExpression(
            Constraints.MurlRegEx,
            ErrorMessage = "Invalid URL address")]
        private string fullAddress;

        private string protocol = defaultProtocol;

        private string address;

        private string port = defaultPort;

        private string pathAndQuery;

        public MediaPlayer MediaPlayer { get; }

        //[ObservableProperty]
        //private string buttonTitle = string.Empty;

        public bool CanExecutePlayPause
        {
            get => !string.IsNullOrWhiteSpace(protocol) && !string.IsNullOrWhiteSpace(address);
        }

        [ObservableProperty]        
        private bool isPlaying = false;        

        private readonly SettingsViewModel settingsViewModel;

        [ObservableProperty]
        private bool pinVideoWindowToMainWindow = true;        

        [ObservableProperty]
        private int cameraStreamTimeout = 600;

        [ObservableProperty]
        private bool remoteConnection = false;

        [ObservableProperty]
        private bool isUVCCamera = true;        

        [ObservableProperty]
        private bool isNotWindows = !OperatingSystem.IsWindows();

        public StreamViewModel(IDialogService dialogService, IVideoStreamService videoStreamService, SettingsViewModel settingsViewModel)
        {
            this.dialogService = dialogService;
            this.settingsViewModel = settingsViewModel;
            this.videoStreamService = videoStreamService;

            PinVideoWindowToMainWindow = settingsViewModel.PinVideoWindowToMainWindow;
            CameraStreamTimeout = settingsViewModel.CameraStreamTimeout;

            address = AppService.GetLocalIPAddress() ?? string.Empty;
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

            //ButtonTitle = DynRes.TryGet("Play");
        }

        private void LibVLC_Log(object? sender, LogEventArgs e)
        {
            logger.Debug(e.Message);
        }

        private void Play()
        {
            if (!string.IsNullOrWhiteSpace(protocol) && !string.IsNullOrWhiteSpace(address))
            {
                if (address == AppService.GetLocalIPAddress())
                {
                    string uvcDevice = "v4l2:///dev/video0";
                    if (OperatingSystem.IsWindows() && !RemoteConnection)
                    {
                        uvcDevice = "dshow://";
                    }

                    videoStreamService.OpenVideoStream(uvcDevice, IsUVCCamera, $"{defaultLocalAddress}:{defaultPort}");                    
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
            //ButtonTitle = DynRes.TryGet("Stop");
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
            //ButtonTitle = DynRes.TryGet("Play");
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

            videoStreamService.CloseVideoStream(IsUVCCamera);
        }

        private string GetFullUrlFromParts()
        {
            string newRemoteAddress = address;
            string addr = newRemoteAddress;
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
            string url = Constraints.MurlRegEx;

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

        partial void OnIsUVCCameraChanged(bool oldValue, bool newValue)
        {
            protocol = newValue ? tcpH264Protocol : defaultProtocol;
        }

        [RelayCommand]
        private void ResetAddress()
        {
            address = AppService.GetLocalIPAddress() ?? string.Empty;
            protocol = defaultProtocol;
            port = defaultPort;
            FullAddress = GetFullUrlFromParts();
        }
    }
}
