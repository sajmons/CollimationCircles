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
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CollimationCircles.ViewModels
{
    public partial class StreamViewModel : BaseViewModel, IViewClosed
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly ICameraControlService cameraControlService;
        private readonly ICameraService cameraService;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
        //[RegularExpression(
        //    Constraints.MurlRegEx,
        //    ErrorMessage = "Invalid URL address")]
        private string fullAddress = string.Empty;

        public bool CanExecutePlayPause
        {
            get => SelectedCamera is not null && !string.IsNullOrEmpty(SelectedCamera.Name);
        }

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(BinningIncreaseCommand))]
        [NotifyCanExecuteChangedFor(nameof(BinningDecreaseCommand))]
        [NotifyCanExecuteChangedFor(nameof(ResetControlsCommand))]
        [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
        [NotifyCanExecuteChangedFor(nameof(ToggleServerConnectionCommand))]
        [NotifyPropertyChangedFor(nameof(CanEditServer))]
        private bool isPlaying = false;

        private readonly SettingsViewModel settingsViewModel;

        [ObservableProperty]
        private bool pinVideoWindowToMainWindow = true;

        [ObservableProperty]
        private bool remoteConnection = false;

        [ObservableProperty]
        private INotifyPropertyChanged? settingsDialogViewModel;

        [ObservableProperty]
        private ObservableCollection<Camera> cameraList = [];

        public bool HasCameras => CameraList.Count > 0;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(PlayPauseCommand))]
        private Camera selectedCamera = new();

        [ObservableProperty]
        string alpacaServerAddress = "127.0.0.1";

        [ObservableProperty]
        int alpacaServerPort = 11111;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ConnectButtonText))]
        [NotifyPropertyChangedFor(nameof(CanEditServer))]
        private bool isServerConnected;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanEditServer))]
        [NotifyCanExecuteChangedFor(nameof(ToggleServerConnectionCommand))]
        private bool isServerBusy;

        [ObservableProperty]
        private bool isCaptureProgressVisible;

        [ObservableProperty]
        private bool isCaptureProgressIndeterminate;

        [ObservableProperty]
        private double captureProgressValue;

        [ObservableProperty]
        private string captureProgressText = "Waiting for frame...";

        public string ConnectButtonText => IsServerConnected ? "Disconnect" : "Connect";

        public bool CanEditServer => !IsServerConnected && !IsServerBusy && !IsPlaying;

        public StreamViewModel()
        {
            this.cameraService = Ioc.Default.GetRequiredService<ICameraService>();
            this.settingsViewModel = Ioc.Default.GetRequiredService<SettingsViewModel>();
            this.cameraControlService = Ioc.Default.GetRequiredService<ICameraControlService>();
            FullAddress = this.cameraService.FullAddress;

            PinVideoWindowToMainWindow = settingsViewModel.PinVideoWindowToMainWindow;
            alpacaServerAddress = settingsViewModel.AlpacaServerAddress;
            AlpacaServerPort = settingsViewModel.AlpacaServerPort;

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

            WeakReferenceMessenger.Default.Register<CameraCaptureProgressMessage>(this, (r, m) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var progress = m.Value;
                    IsCaptureProgressVisible = true;
                    IsCaptureProgressIndeterminate = progress.Stage == CameraCaptureStage.Downloading;
                    CaptureProgressValue = progress.Progress;
                    CaptureProgressText = progress.Status;
                });
            });

            CameraList.CollectionChanged += CameraList_CollectionChanged;
        }

        partial void OnCameraListChanged(ObservableCollection<Camera> oldValue, ObservableCollection<Camera> newValue)
        {
            if (oldValue is not null)
            {
                oldValue.CollectionChanged -= CameraList_CollectionChanged;
            }

            if (newValue is not null)
            {
                newValue.CollectionChanged += CameraList_CollectionChanged;
            }

            OnPropertyChanged(nameof(HasCameras));
        }

        private void CameraList_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasCameras));
        }

        private void MediaPlayer_Opening()
        {
            logger.Trace($"MediaPlayer opening");

            ShowWebCamStream();
            IsPlaying = SelectedCamera?.APIType is APIType.Alpaca || cameraService.IsPlaying;
            if (SelectedCamera is not null)
            {
                SelectedCamera.IsPlaying = IsPlaying;
            }
        }

        private void MediaPlayer_Playing()
        {
            Guard.IsNotNull(SelectedCamera);

            logger.Trace($"MediaPlayer playing");
            IsPlaying = SelectedCamera.APIType is APIType.Alpaca || cameraService.IsPlaying;
            SelectedCamera.IsPlaying = IsPlaying;
        }

        private void MediaPlayer_Closed()
        {
            logger.Trace($"MediaPlayer closed");

            CloseWebCamStream();
            IsPlaying = cameraService.IsPlaying;
            if (SelectedCamera is not null)
            {
                SelectedCamera.IsPlaying = IsPlaying;
            }
        }

        [RelayCommand(CanExecute = nameof(CanExecutePlayPause))]
        private async Task PlayPause()
        {
            await StartSelectedCameraAsync();
        }

        private async Task StartSelectedCameraAsync()
        {
            Guard.IsNotNull(SelectedCamera);

            logger.Info($"PlayPause clicked: camera='{SelectedCamera.Name}', APIType={SelectedCamera.APIType}, FullAddress='{FullAddress}'");            

            if (SelectedCamera.APIType is APIType.Alpaca)
            {
                SelectedCamera.ServerAddress = AlpacaServerAddress.Trim();
                SelectedCamera.ServerPort = AlpacaServerPort;
            }

            if (!cameraService.IsPlaying)
            {
                await cameraService.Play(SelectedCamera);
            }
            else
            {
                await cameraService.Stop(SelectedCamera);
            }
        }        

        public bool CanExecuteZoom
        {
            get => IsPlaying;
        }

        private void ChangeBinning(int delta)
        {
            if (SelectedCamera?.Controls.FirstOrDefault(c => c.Name == ControlType.Binning) is not ICameraControl bin)
            {
                return;
            }

            double next = Math.Clamp(bin.Value + delta, bin.Min, bin.Max);
            if (next != bin.Value)
            {
                bin.Value = next;
            }
        }

        [RelayCommand(CanExecute = nameof(CanExecuteZoom))]
        private void BinningIncrease() => ChangeBinning(1);

        [RelayCommand(CanExecute = nameof(CanExecuteZoom))]
        private void BinningDecrease() => ChangeBinning(-1);

        [RelayCommand(CanExecute = nameof(CanExecuteZoom))]
        private void ResetControls()
        {
            SelectedCamera?.SetDefaultControls();
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

        [RelayCommand(CanExecute = nameof(CanExecuteToggleServer))]
        private async Task ToggleServerConnection()
        {
            IsServerBusy = true;
            try
            {
                if (IsServerConnected)
                {
                    if (IsPlaying)
                    {
                        await cameraControlService.StopCamera(SelectedCamera);
                    }

                    await cameraControlService.DisconnectServer();
                    CameraList = [];
                    SelectedCamera = new();
                    IsServerConnected = false;
                    return;
                }

                var address = AlpacaServerAddress.Trim();
                var port = AlpacaServerPort;
                settingsViewModel.AlpacaServerAddress = address;
                settingsViewModel.AlpacaServerPort = port;

                var cameras = await cameraControlService.ConnectServer(address, port);
                CameraList = new ObservableCollection<Camera>(cameras);
                SelectedCamera = CameraList.FirstOrDefault(c => c.Name == settingsViewModel.LastSelectedCamera) ?? CameraList.FirstOrDefault() ?? new();
                IsServerConnected = true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Alpaca server connect/disconnect failed");
                IsServerConnected = false;
            }
            finally
            {
                IsServerBusy = false;
            }
        }

        private bool CanExecuteToggleServer() => !IsServerBusy && !IsPlaying;

        public void OnClosed()
        {
            //SettingsDialogViewModel = null;
        }

        partial void OnSelectedCameraChanged(Camera? oldValue, Camera newValue)
        {
            if (newValue is not null)
            {
                FullAddress = cameraService.DefaultAddress(newValue);
                RemoteConnection = SelectedCamera?.APIType == APIType.Remote;
                this.settingsViewModel.LastSelectedCamera = newValue.Name;
                newValue.IsPlaying = IsPlaying;
                logger.Info($"Selected camera changed to '{newValue.Name}'");
            }
        }

        partial void OnFullAddressChanged(string? oldValue, string newValue)
        {
            FullAddress = newValue;
            cameraService.FullAddress = newValue;
        }

        [RelayCommand]
        private void Default()
        {
            SelectedCamera?.SetDefaultControls();
            logger.Info("Default camera controls command ececuted");
        }
    }
}
