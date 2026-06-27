using Avalonia.Threading;
using CollimationCircles.Messages;
using CollimationCircles.Models;
using CollimationCircles.Services;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;

namespace CollimationCircles.ViewModels
{
    public partial class StreamViewModel : BaseViewModel, IViewClosed
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
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
            // Note: we intentionally do NOT check libVLCService.IsAvailable here.
            // LibVLC is initialised lazily on first Play attempt; checking IsAvailable
            // before that would always be false and grey-out the Play button forever.
            get => !string.IsNullOrWhiteSpace(FullAddress) && SelectedCamera is not null;
        }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ZoomInCommand))]
        [NotifyCanExecuteChangedFor(nameof(ZoomOutCommand))]
        [NotifyCanExecuteChangedFor(nameof(ZoomResetCommand))]
        private bool isPlaying = false;

        private readonly SettingsViewModel settingsViewModel;

        [ObservableProperty]
        private bool pinVideoWindowToMainWindow = true;

        [ObservableProperty]
        private bool remoteConnection = false;

        [ObservableProperty]
        private bool isWindows = OperatingSystem.IsWindows();

        [ObservableProperty]
        private INotifyPropertyChanged? settingsDialogViewModel;

        [ObservableProperty]
        private ObservableCollection<Camera> cameraList = [];

        [ObservableProperty]
        private Camera selectedCamera = new();

        [ObservableProperty]
        bool displayAdvancedDShowDialog = false;

        public StreamViewModel()
        {
            this.libVLCService = Ioc.Default.GetRequiredService<ILibVLCService>();
            this.settingsViewModel = Ioc.Default.GetRequiredService<SettingsViewModel>();
            this.cameraControlService = Ioc.Default.GetRequiredService<ICameraControlService>();
            FullAddress = this.libVLCService.FullAddress;

            PinVideoWindowToMainWindow = settingsViewModel.PinVideoWindowToMainWindow;

            Dispatcher.UIThread.Post(async () =>
            {
                CameraList = [.. await cameraControlService.GetCameraList()];
                SelectedCamera = CameraList?.Where(c => c.Name == settingsViewModel.LastSelectedCamera)?.FirstOrDefault() ?? CameraList?.FirstOrDefault() ?? new();
            });

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
            IsPlaying = SelectedCamera?.APIType == APIType.Zwo || libVLCService.MediaPlayer?.IsPlaying == true;
            if (SelectedCamera is not null)
            {
                SelectedCamera.IsPlaying = IsPlaying;
            }
        }

        private void MediaPlayer_Playing()
        {
            Guard.IsNotNull(SelectedCamera);

            logger.Trace($"MediaPlayer playing");
            IsPlaying = SelectedCamera.APIType == APIType.Zwo || libVLCService.MediaPlayer?.IsPlaying == true;
            SelectedCamera.IsPlaying = IsPlaying;
        }

        private void MediaPlayer_Closed()
        {
            logger.Trace($"MediaPlayer closed");

            CloseWebCamStream();
            IsPlaying = libVLCService.MediaPlayer?.IsPlaying == true;
            if (SelectedCamera is not null)
            {
                SelectedCamera.IsPlaying = IsPlaying;
            }
        }

        [RelayCommand(CanExecute = nameof(CanExecutePlayPause))]
        private void PlayPause()
        {
            Guard.IsNotNull(SelectedCamera);

            // ZWO cameras use a direct-rendering path that bypasses LibVLC entirely.
            if (SelectedCamera.APIType == APIType.Zwo)
            {
                var zwoFrameSource = Ioc.Default.GetRequiredService<IZwoFrameSource>();

                if (zwoFrameSource.IsStreaming)
                {
                    zwoFrameSource.Stop();
                    MediaPlayer_Closed();
                }
                else
                {
                    libVLCService.Play(SelectedCamera, DisplayAdvancedDShowDialog);
                }

                return;
            }

            // Play() will lazily initialise LibVLC and return early if it is not
            // available.  We fall through to the compatibility message below when
            // IsAvailable is still false after the call.
            if (libVLCService.MediaPlayer != null && libVLCService.IsAvailable)
            {
                if (!libVLCService.MediaPlayer.IsPlaying)
                {
                    libVLCService.Play(SelectedCamera, DisplayAdvancedDShowDialog);
                }
                else
                {
                    libVLCService.MediaPlayer.Stop();
                }

                return;
            }

            // First attempt: try to start the stream (this triggers lazy init).
            if (!libVLCService.IsAvailable)
            {
                libVLCService.Play(SelectedCamera, DisplayAdvancedDShowDialog);
            }

            if (!libVLCService.IsAvailable)
            {
                logger.Warn("Play requested but LibVLC is not available.");
                _ = ShowLibVlcCompatibilityMessageAsync();
            }
        }

        private async Task ShowLibVlcCompatibilityMessageAsync()
        {
            string message =
                ResSvc.TryGetString("LibVlcCompatibilityBody1") + "\n\n" +
                ResSvc.TryGetString("LibVlcCompatibilityBody2") + "\n" +
                ResSvc.TryGetString("LibVlcCompatibilityBody3") + "\n" +
                ResSvc.TryGetString("LibVlcCompatibilityBody4") + "\n" +
                ResSvc.TryGetString("LibVlcCompatibilityBody5") + "\n" +
                ResSvc.TryGetString("LibVlcCompatibilityBody6");

            await DialogService.ShowMessageBoxAsync(null,
                message,
                ResSvc.TryGetString("LibVlcCompatibilityTitle"),
                MessageBoxButton.Ok);
        }

        public bool CanExecuteZoom
        {
            get => IsPlaying;
        }

        [RelayCommand(CanExecute = nameof(CanExecuteZoom))]
        private void ZoomIn()
        {
            WeakReferenceMessenger.Default.Send(new ImageZoomMessage(ImageZoomAction.In));
        }

        [RelayCommand(CanExecute = nameof(CanExecuteZoom))]
        private void ZoomOut()
        {
            WeakReferenceMessenger.Default.Send(new ImageZoomMessage(ImageZoomAction.Out));
        }

        [RelayCommand(CanExecute = nameof(CanExecuteZoom))]
        private void ZoomReset()
        {
            WeakReferenceMessenger.Default.Send(new ImageZoomMessage(ImageZoomAction.Reset));
        }

        private void ShowWebCamStream()
        {
            Dispatcher.UIThread.Post(() =>
            {
                DialogService.Show<StreamViewModel>(null, this);
                logger.Trace($"Opened web camera stream window");
            });
        }

        private void CloseWebCamStream()
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    DialogService.Close(this);
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

        partial void OnIsPlayingChanged(bool oldValue, bool newValue)
        {
            if (SelectedCamera is not null)
            {
                SelectedCamera.IsPlaying = newValue;
            }
        }

        [RelayCommand]
        private async Task CameraRefresh()
        {
            CameraList = new ObservableCollection<Camera>(await cameraControlService.GetCameraList());
            SelectedCamera = CameraList.FirstOrDefault(c => c.Name == settingsViewModel.LastSelectedCamera) ?? CameraList.First();
        }        

        public void OnClosed()
        {
            //SettingsDialogViewModel = null;
        }

        partial void OnSelectedCameraChanged(Camera? oldValue, Camera newValue)
        {
            if (newValue is not null)
            {
                FullAddress = libVLCService.DefaultAddress(newValue);
                RemoteConnection = SelectedCamera?.APIType == APIType.Remote;
                this.settingsViewModel.LastSelectedCamera = newValue.Name;
                newValue.IsPlaying = IsPlaying;
                logger.Info($"Selected camera changed to '{newValue.Name}'");
            }
        }

        partial void OnFullAddressChanged(string? oldValue, string newValue)
        {
            FullAddress = newValue;
            libVLCService.FullAddress = newValue;
        }

        [RelayCommand]
        private void Default()
        {
            SelectedCamera?.SetDefaultControls();
            logger.Info("Default camera controls command ececuted");
        }
    }
}
