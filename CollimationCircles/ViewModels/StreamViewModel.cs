﻿using Avalonia.Threading;
using CollimationCircles.Messages;
using CollimationCircles.Models;
using CollimationCircles.Services;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HanumanInstitute.MvvmDialogs;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

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
            get => !string.IsNullOrWhiteSpace(FullAddress) && SelectedCamera is not null;
        }

        [ObservableProperty]
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
        private Camera? selectedCamera;

        [ObservableProperty]
        private bool controlsEnabled = false;

        [ObservableProperty]
        bool displayAdvancedDShowDialog = false;

        public StreamViewModel(ILibVLCService libVLCService, ICameraControlService cameraControlService, SettingsViewModel settingsViewModel)
        {
            this.libVLCService = libVLCService;
            this.settingsViewModel = settingsViewModel;
            this.cameraControlService = cameraControlService;

            FullAddress = this.libVLCService.FullAddress;

            PinVideoWindowToMainWindow = settingsViewModel.PinVideoWindowToMainWindow;

            CameraList = new ObservableCollection<Camera>(cameraControlService.GetCameraList());

            SelectedCamera = CameraList?.FirstOrDefault(c => c.Name == settingsViewModel!.LastSelectedCamera) ?? CameraList?.FirstOrDefault() ?? null;

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
            ControlsEnabled = false;
        }

        private void MediaPlayer_Playing()
        {
            Guard.IsNotNull(SelectedCamera);

            logger.Trace($"MediaPlayer playing");
            IsPlaying = libVLCService.MediaPlayer.IsPlaying;
            ControlsEnabled = IsPlaying && SelectedCamera.APIType == APIType.Dshow || SelectedCamera.APIType == APIType.V4l2;
        }

        private void MediaPlayer_Closed()
        {
            logger.Trace($"MediaPlayer closed");

            CloseWebCamStream();
            IsPlaying = libVLCService.MediaPlayer.IsPlaying;
            ControlsEnabled = false;
        }

        [RelayCommand(CanExecute = nameof(CanExecutePlayPause))]
        private void PlayPause()
        {
            Guard.IsNotNull(SelectedCamera);

            if (libVLCService.MediaPlayer != null)
            {
                if (!libVLCService.MediaPlayer.IsPlaying)
                {
                    libVLCService.Play(SelectedCamera, DisplayAdvancedDShowDialog);
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

        [RelayCommand]
        private void CameraSettings()
        {
            if (SettingsDialogViewModel is null)
            {
                SettingsDialogViewModel = DialogService.CreateViewModel<CameraControlsViewModel>();

                if (SettingsDialogViewModel is not null)
                {
                    DialogService.Show(null, SettingsDialogViewModel);
                    logger.Info("Opened camera controls dialog");
                }
            }
            else
            {
                DialogService.Close(SettingsDialogViewModel);
                DialogService.Show(null, SettingsDialogViewModel);
            }
        }

        [RelayCommand]
        private void CameraRefresh()
        {
            CameraList = new ObservableCollection<Camera>(cameraControlService.GetCameraList());
            SelectedCamera = CameraList.FirstOrDefault(c => c.Name == settingsViewModel.LastSelectedCamera) ?? CameraList.First();
        }

        public void OnClosed()
        {
            SettingsDialogViewModel = null;
        }

        partial void OnSelectedCameraChanged(Camera? oldValue, Camera? newValue)
        {
            if (newValue is not null)
            {
                FullAddress = libVLCService.DefaultAddress(newValue);
                RemoteConnection = SelectedCamera?.APIType == APIType.Remote;
                this.settingsViewModel.LastSelectedCamera = newValue.Name;
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
        }
    }
}
