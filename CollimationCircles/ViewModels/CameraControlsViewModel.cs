using CollimationCircles.Messages;
using CollimationCircles.Models;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LibVLCSharp.Shared;

namespace CollimationCircles.ViewModels
{
    public partial class CameraControlsViewModel : BaseViewModel
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly ILibVLCService libVLCService;
        private readonly StreamViewModel streamViewModel;

        [ObservableProperty]
        private Camera camera;

        [ObservableProperty]
        private bool isLibCamera;

        [ObservableProperty]
        private bool isPlaying;

        public CameraControlsViewModel()
        {
            this.libVLCService = Ioc.Default.GetRequiredService<ILibVLCService>();
            this.streamViewModel = Ioc.Default.GetRequiredService<StreamViewModel>();


            Camera = streamViewModel.SelectedCamera;
            IsLibCamera = Camera.APIType == APIType.LibCamera;

            WeakReferenceMessenger.Default.Register<CameraStateMessage>(this, (r, m) =>
            {
                Camera = streamViewModel.SelectedCamera;
                IsLibCamera = Camera.APIType == APIType.LibCamera;
            });

            Title = $"{ResSvc.TryGetString("CollimationCircles")} - {ResSvc.TryGetString("CameraControls")}";

            if (libVLCService.MediaPlayer is not null)
            {
                libVLCService.MediaPlayer.Playing += (sender, e) => IsPlaying = true;
                libVLCService.MediaPlayer.Paused += (sender, e) => IsPlaying = false;
                libVLCService.MediaPlayer.Stopped += (sender, e) => IsPlaying = false;
            }
        }

        [RelayCommand]
        private void Default()
        {
            Camera.SetDefaultControls();
            logger.Info("Default camera controls buton clicked");
        }

        [RelayCommand]
        private void Apply()
        {
            if (!libVLCService.IsAvailable)
            {
                logger.Warn("Cannot apply camera controls because LibVLC is not available.");
                return;
            }

            libVLCService.Play(Camera, false);
            logger.Info("Apply camera controls buton clicked");
        }
    }
}