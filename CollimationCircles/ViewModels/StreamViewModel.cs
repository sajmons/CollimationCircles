using System;
using Avalonia.Threading;
using CollimationCircles.Messages;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HanumanInstitute.MvvmDialogs;
using System.ComponentModel;

namespace CollimationCircles.ViewModels
{
    public partial class StreamViewModel : BaseViewModel, IViewClosed
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly IDialogService dialogService;
        private readonly ICameraControlService cameraControlService;
        private readonly ILibVLCService libVLCService;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
        //[RegularExpression(
        //    Constraints.MurlRegEx,
        //    ErrorMessage = "Invalid URL address")]
        private string fullAddress = string.Empty;

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

        [ObservableProperty]
        private INotifyPropertyChanged? settingsDialogViewModel;

        public StreamViewModel(ILibVLCService libVLCService, IDialogService dialogService, ICameraControlService cameraControlService, SettingsViewModel settingsViewModel)
        {
            this.libVLCService = libVLCService;
            this.dialogService = dialogService;
            this.settingsViewModel = settingsViewModel;
            this.cameraControlService = cameraControlService;

            FullAddress = this.libVLCService.FullAddress;

            PinVideoWindowToMainWindow = settingsViewModel.PinVideoWindowToMainWindow;
            CameraStreamTimeout = settingsViewModel.CameraStreamTimeout;

            WeakReferenceMessenger.Default.Register<CameraStateMessage>(this, (r, m) =>
            {
                switch (m.Value)
                {
                    case CameraState.Opening:
                        MediaPlayer_Opening();
                        break;
                    case CameraState.Stopped:
                        MediaPlayer_Closed();
                        break;
                    case CameraState.Playing:
                        MediaPlayer_Playing();
                        break;
                }
            });
        }

        private void MediaPlayer_Opening()
        {
            logger.Trace($"MediaPlayer opening");

            ShowWebCamStream();
            IsPlaying = libVLCService.MediaPlayer.IsPlaying;
        }

        private void MediaPlayer_Playing()
        {
            logger.Trace($"MediaPlayer playing");
            IsPlaying = libVLCService.MediaPlayer.IsPlaying;
        }

        private void MediaPlayer_Closed()
        {
            logger.Trace($"MediaPlayer closed");

            CloseWebCamStream();
            IsPlaying = libVLCService.MediaPlayer.IsPlaying;
        }

        [RelayCommand(CanExecute = nameof(CanExecutePlayPause))]
        private void PlayPause()
        {
            if (libVLCService.MediaPlayer != null)
            {
                if (!libVLCService.MediaPlayer.IsPlaying)
                {
                    libVLCService.Play();
                }
                else
                {
                    libVLCService.MediaPlayer.Stop();
                }
            }
        }

        private void ShowWebCamStream()
        {
            Dispatcher.UIThread.Post(() =>
            {
                dialogService?.Show<StreamViewModel>(null, this);
                cameraControlService.Open();
                logger.Trace($"Opened web camera stream window");
            });
        }

        private void CloseWebCamStream()
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    cameraControlService.Release();
                    dialogService?.Close(this);
                    logger.Trace($"Closed web camera stream window");
                }
                catch (Exception exc)
                {
                    logger.Warn($"Unable to close video dialog. Probably closed by a user. {exc.Message}");
                }
            });
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
            StreamSource ss = StreamSource.UVC;

            if (IsRaspberryPi)
                ss = StreamSource.RaspberryPi;

            if (IsRemote)
                ss = StreamSource.Remote;

            FullAddress = libVLCService.DefaultAddress(ss);
        }

        partial void OnIsUVCChanged(bool value)
        {
            FullAddress = libVLCService.DefaultAddress(value ? StreamSource.UVC : StreamSource.Undefined);
        }

        partial void OnIsRaspberryPiChanged(bool value)
        {
            FullAddress = libVLCService.DefaultAddress(value ? StreamSource.RaspberryPi : StreamSource.Undefined);
        }

        partial void OnIsRemoteChanged(bool value)
        {
            FullAddress = libVLCService.DefaultAddress(value ? StreamSource.Remote : StreamSource.Undefined);
        }

        partial void OnFullAddressChanged(string value)
        {
            libVLCService.FullAddress = value;
        }

        [RelayCommand]
        private void CameraSettings()
        {
            if (SettingsDialogViewModel is null)
            {
                SettingsDialogViewModel = dialogService?.CreateViewModel<CameraControlsViewModel>();

                if (SettingsDialogViewModel is not null)
                {
                    dialogService?.Show(null, SettingsDialogViewModel);
                    logger.Info("Opened camera settings window");
                }
            }
            else
            {
                dialogService?.Close(SettingsDialogViewModel);
                dialogService?.Show(null, SettingsDialogViewModel);
            }
        }

        public void OnClosed()
        {
            SettingsDialogViewModel = null;
        }
    }
}
