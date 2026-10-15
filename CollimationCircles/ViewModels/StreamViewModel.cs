using Avalonia.Threading;
using CollimationCircles.Messages;
using CollimationCircles.Models;
using CollimationCircles.Services.Alpaca;
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
        private readonly AlpacaTelescopeClient alpacaTelescopeClient = new();
        private int telescopeMotionStartInProgress;
        private bool suppressTelescopeStateDispatch;
        private ObservableCollection<TelescopeMount>? trackedTelescopeMounts;

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

        [ObservableProperty]
        public SettingsViewModel settingsViewModel;

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
        [NotifyPropertyChangedFor(nameof(CanControlTelescope))]
        private bool isServerConnected;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanEditServer))]
        [NotifyCanExecuteChangedFor(nameof(ToggleServerConnectionCommand))]
        [NotifyPropertyChangedFor(nameof(CanControlTelescope))]
        private bool isServerBusy;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasTelescopeMounts))]
        [NotifyPropertyChangedFor(nameof(CanControlTelescope))]
        private ObservableCollection<TelescopeMount> telescopeMounts = [];

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanControlTelescope))]
        private TelescopeMount? selectedTelescopeMount;

        [ObservableProperty]
        private bool telescopeTracking;

        [ObservableProperty]
        private bool telescopeParked;

        [ObservableProperty]
        private bool invertTelescopeUpDown;

        [ObservableProperty]
        private bool invertTelescopeLeftRight;

        [ObservableProperty]
        private ObservableCollection<double> telescopeSpeeds = [0.25d, 0.5d, 1.0d, 2.0d];

        [ObservableProperty]
        private double selectedTelescopeSpeed = 0.5d;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanControlTelescope))]
        private bool isTelescopeBusy;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanControlTelescope))]
        private bool isTelescopeMoving;

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

        public bool HasTelescopeMounts => TelescopeMounts.Count > 0;

        public bool CanControlTelescope =>
            IsServerConnected
            && !IsServerBusy
            && !IsTelescopeBusy
            && SelectedTelescopeMount is not null
            && HasTelescopeMounts;

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
            TelescopeMounts.CollectionChanged += TelescopeMounts_CollectionChanged;
        }

        partial void OnCameraListChanged(ObservableCollection<Camera>? oldValue, ObservableCollection<Camera> newValue)
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

        partial void OnTelescopeMountsChanged(ObservableCollection<TelescopeMount> value)
        {
            if (trackedTelescopeMounts is not null)
            {
                trackedTelescopeMounts.CollectionChanged -= TelescopeMounts_CollectionChanged;
            }

            trackedTelescopeMounts = value;

            if (trackedTelescopeMounts is not null)
            {
                trackedTelescopeMounts.CollectionChanged += TelescopeMounts_CollectionChanged;
            }

            OnPropertyChanged(nameof(HasTelescopeMounts));
            OnPropertyChanged(nameof(CanControlTelescope));
        }

        private void CameraList_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasCameras));
        }

        private void TelescopeMounts_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(HasTelescopeMounts));
            OnPropertyChanged(nameof(CanControlTelescope));
        }

        partial void OnSelectedTelescopeMountChanged(TelescopeMount? value)
        {
            OnPropertyChanged(nameof(CanControlTelescope));
            StartTelescopeMotionCommand.NotifyCanExecuteChanged();
            StopTelescopeMotionCommand.NotifyCanExecuteChanged();

            if (value is null)
            {
                suppressTelescopeStateDispatch = true;
                TelescopeTracking = false;
                TelescopeParked = false;
                suppressTelescopeStateDispatch = false;
                return;
            }

            _ = SelectTelescopeMountAsync(value);
        }

        partial void OnTelescopeParkedChanged(bool value)
        {
            StartTelescopeMotionCommand.NotifyCanExecuteChanged();

            if (suppressTelescopeStateDispatch || SelectedTelescopeMount is null)
            {
                return;
            }

            _ = SetParkedStateAsync(value);
        }

        partial void OnTelescopeTrackingChanged(bool value)
        {
            if (suppressTelescopeStateDispatch || SelectedTelescopeMount is null)
            {
                return;
            }

            _ = SetTrackingStateAsync(value);
        }

        partial void OnIsTelescopeBusyChanged(bool value)
        {
            OnPropertyChanged(nameof(CanControlTelescope));
            StartTelescopeMotionCommand.NotifyCanExecuteChanged();
            StopTelescopeMotionCommand.NotifyCanExecuteChanged();
        }

        partial void OnIsServerConnectedChanged(bool value)
        {
            OnPropertyChanged(nameof(CanControlTelescope));
            StartTelescopeMotionCommand.NotifyCanExecuteChanged();
            StopTelescopeMotionCommand.NotifyCanExecuteChanged();
        }

        private void MediaPlayer_Opening()
        {
            logger.Trace($"MediaPlayer opening");

            ShowWebCamStream();
            IsPlaying = SelectedCamera?.APIType is APIType.Alpaca || cameraService.IsPlaying;
            SelectedCamera?.IsPlaying = IsPlaying;
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
            SelectedCamera?.IsPlaying = IsPlaying;
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
                    await ResetTelescopeStateAsync();
                    IsServerConnected = false;
                    return;
                }

                var address = AlpacaServerAddress.Trim();
                var port = AlpacaServerPort;
                SettingsViewModel.AlpacaServerAddress = address;
                SettingsViewModel.AlpacaServerPort = port;

                var cameras = await cameraControlService.ConnectServer(address, port);
                CameraList = new ObservableCollection<Camera>(cameras);
                SelectedCamera = CameraList.FirstOrDefault(c => c.Name == SettingsViewModel.LastSelectedCamera) ?? CameraList.FirstOrDefault() ?? new();
                await RefreshTelescopeMountsAsync(address, port);
                IsServerConnected = true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Alpaca server connect/disconnect failed");
                IsServerConnected = false;
                await ResetTelescopeStateAsync();
            }
            finally
            {
                IsServerBusy = false;
            }
        }

        private bool CanExecuteToggleServer() => !IsServerBusy && !IsPlaying;

        private bool CanExecuteTelescopeMotion() => CanControlTelescope && !IsTelescopeMoving && !TelescopeParked;

        private bool CanExecuteStopTelescopeMotion() => CanControlTelescope && IsTelescopeMoving;

        [RelayCommand(CanExecute = nameof(CanExecuteTelescopeMotion))]
        private async Task StartTelescopeMotion(TelescopeDirection direction)
        {
            if (Interlocked.CompareExchange(ref telescopeMotionStartInProgress, 1, 0) != 0)
            {
                return;
            }

            if (SelectedTelescopeMount is null)
            {
                Interlocked.Exchange(ref telescopeMotionStartInProgress, 0);
                return;
            }

            var mappedDirection = MapDirection(direction);
            IsTelescopeBusy = true;
            try
            {
                await alpacaTelescopeClient.StartMotion(mappedDirection, SelectedTelescopeSpeed);
                IsTelescopeMoving = true;
            }
            catch (Exception ex)
            {
                IsTelescopeMoving = false;
                logger.Error(ex, $"Failed to start telescope motion for direction '{mappedDirection}'.");
            }
            finally
            {
                IsTelescopeBusy = false;
                Interlocked.Exchange(ref telescopeMotionStartInProgress, 0);
            }
        }

        [RelayCommand(CanExecute = nameof(CanExecuteStopTelescopeMotion))]
        private async Task StopTelescopeMotion()
        {
            if (SelectedTelescopeMount is null)
            {
                return;
            }

            IsTelescopeBusy = true;
            try
            {
                await alpacaTelescopeClient.StopMotion();
                IsTelescopeMoving = false;
            }
            finally
            {
                IsTelescopeBusy = false;
            }
        }

        [RelayCommand]
        private async Task StopTelescope()
        {
            await StopTelescopeMotion();
        }

        private async Task SelectTelescopeMountAsync(TelescopeMount mount)
        {
            IsTelescopeBusy = true;
            try
            {
                alpacaTelescopeClient.SelectMount(mount);
                var tracking = await alpacaTelescopeClient.GetTrackingAsync();
                var parked = await alpacaTelescopeClient.GetParkedAsync();

                suppressTelescopeStateDispatch = true;
                TelescopeTracking = tracking;
                TelescopeParked = parked;
                suppressTelescopeStateDispatch = false;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to select telescope mount");
            }
            finally
            {
                IsTelescopeBusy = false;
            }
        }

        private async Task SetTrackingStateAsync(bool enabled)
        {
            try
            {
                IsTelescopeBusy = true;
                await alpacaTelescopeClient.SetTrackingAsync(enabled);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to change telescope tracking state");
            }
            finally
            {
                IsTelescopeBusy = false;
            }
        }

        private async Task SetParkedStateAsync(bool parked)
        {
            try
            {
                IsTelescopeBusy = true;
                if (parked)
                {
                    await alpacaTelescopeClient.ParkAsync();
                    IsTelescopeMoving = false;
                }
                else
                {
                    await alpacaTelescopeClient.UnparkAsync();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to change telescope parked state");
            }
            finally
            {
                IsTelescopeBusy = false;
            }
        }

        private async Task RefreshTelescopeMountsAsync(string address, int port)
        {
            try
            {
                var mounts = await alpacaTelescopeClient.DiscoverMountsAsync(address, port);
                TelescopeMounts = new ObservableCollection<TelescopeMount>(mounts);
                SelectedTelescopeMount = TelescopeMounts.FirstOrDefault();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to discover telescope mounts");
                await ResetTelescopeStateAsync();
            }
        }

        private Task ResetTelescopeStateAsync()
        {
            suppressTelescopeStateDispatch = true;
            TelescopeMounts = [];
            SelectedTelescopeMount = null;
            TelescopeTracking = false;
            TelescopeParked = false;
            IsTelescopeMoving = false;
            suppressTelescopeStateDispatch = false;
            alpacaTelescopeClient.ClearSelectedMount();
            return Task.CompletedTask;
        }

        private TelescopeDirection MapDirection(TelescopeDirection requestedDirection)
        {
            var mapped = requestedDirection;

            if (InvertTelescopeUpDown)
            {
                mapped = mapped switch
                {
                    TelescopeDirection.Up => TelescopeDirection.Down,
                    TelescopeDirection.UpLeft => TelescopeDirection.DownLeft,
                    TelescopeDirection.UpRight => TelescopeDirection.DownRight,
                    TelescopeDirection.Down => TelescopeDirection.Up,
                    TelescopeDirection.DownLeft => TelescopeDirection.UpLeft,
                    TelescopeDirection.DownRight => TelescopeDirection.UpRight,
                    _ => mapped
                };
            }

            if (InvertTelescopeLeftRight)
            {
                mapped = mapped switch
                {
                    TelescopeDirection.Left => TelescopeDirection.Right,
                    TelescopeDirection.UpLeft => TelescopeDirection.UpRight,
                    TelescopeDirection.DownLeft => TelescopeDirection.DownRight,
                    TelescopeDirection.Right => TelescopeDirection.Left,
                    TelescopeDirection.UpRight => TelescopeDirection.UpLeft,
                    TelescopeDirection.DownRight => TelescopeDirection.DownLeft,
                    _ => mapped
                };
            }

            return mapped;
        }

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
