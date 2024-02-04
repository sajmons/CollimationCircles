using Avalonia.Threading;
using CollimationCircles.Helper;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Threading;

namespace CollimationCircles.ViewModels
{
    public partial class StreamViewModel : BaseViewModel
    {
        private const string rpiPort = "49555";

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly IDialogService dialogService;
        private readonly LibVLC libVLC;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
        //[RegularExpression(
        //    Constraints.MurlRegEx,
        //    ErrorMessage = "Invalid URL address")]
        private string fullAddress = string.Empty;

        private string protocol = string.Empty;

        private string address = string.Empty;

        private string port = string.Empty;

        private string pathAndQuery = string.Empty;

        public MediaPlayer MediaPlayer { get; }

        public bool CanExecutePlayPause
        {
            get => !string.IsNullOrWhiteSpace(FullAddress);
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
        private bool isUVC = true;

        [ObservableProperty]
        private bool isRaspberryPi = false;

        [ObservableProperty]
        private bool isRemote = false;

        [ObservableProperty]
        private bool isWindows = OperatingSystem.IsWindows();        

        public StreamViewModel(IDialogService dialogService, SettingsViewModel settingsViewModel)
        {
            this.dialogService = dialogService;
            this.settingsViewModel = settingsViewModel;
            
            PinVideoWindowToMainWindow = settingsViewModel.PinVideoWindowToMainWindow;
            CameraStreamTimeout = settingsViewModel.CameraStreamTimeout;

            //address = AppService.GetLocalIPAddress() ?? string.Empty;
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
        }

        private void LibVLC_Log(object? sender, LogEventArgs e)
        {
            logger.Debug(e.Message);
        }

        private void Play()
        {
            if (IsRaspberryPi)
            {
                //rpicam-vid -t 0 --inline -n -o udp://0.0.0.0:5000

                List<string> parameters = [
                    "-t",
                    "0",
                    "--inline",
                    "--listen",
                    "-n",
                    "-o",
                    $"tcp://0.0.0.0:{rpiPort}"
                ];

                AppService.ExecuteCommand(
                    "rpicam-vid",
                    parameters, timeout: 0);

                Thread.Sleep(1000);
            }

            if (!string.IsNullOrWhiteSpace(FullAddress))
            {
                MediaPlayerPlay();
            }
        }

        private void MediaPlayerPlay()
        {
            string mrl = FullAddress;

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
                try
                {
                    dialogService?.Close(this);
                    logger.Trace($"Closed web camera stream window");
                }
                catch (Exception exc)
                {
                    logger.Warn($"Unable to close video dialog. Probably closed by a user. {exc.Message}");
                }
            });
        }

        private string GetFullUrlFromParts()
        {
            pathAndQuery = string.Empty;
            port = string.Empty;
            address = string.Empty;

            if (IsUVC)
            {
                if (OperatingSystem.IsWindows())
                {
                    protocol = "dshow";
                }
                else if (OperatingSystem.IsMacOS())
                {
                    protocol = "qtcapture";
                }
                else
                {
                    protocol = "v4l2";
                    address = "/dev/video0";
                }
            }
            if (IsRaspberryPi)
            {
                protocol = "tcp/h264";
                address = "localhost";
                port = rpiPort;
            }
            else if (IsRemote)
            {
                protocol = "http";
            }

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

        [RelayCommand]
        private void ResetAddress()
        {
            FullAddress = GetFullUrlFromParts();
        }

        partial void OnIsUVCChanged(bool oldValue, bool newValue)
        {
            FullAddress = GetFullUrlFromParts();
        }

        partial void OnIsRaspberryPiChanged(bool oldValue, bool newValue)
        {
            FullAddress = GetFullUrlFromParts();
        }

        partial void OnIsRemoteChanged(bool oldValue, bool newValue)
        {
            FullAddress = GetFullUrlFromParts();
        }
    }
}
