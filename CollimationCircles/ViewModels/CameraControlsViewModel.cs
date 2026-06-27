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

                // Drive IsPlaying from the camera state message. The MediaPlayer is
                // lazily initialised (null at construction time), so subscribing to its
                // events directly in the constructor never works. The messenger is the
                // same source StreamViewModel uses to track play state.
                IsPlaying = m.Value == CameraState.Playing;
            });

            Title = $"{ResSvc.TryGetString("CollimationCircles")} - {ResSvc.TryGetString("CameraControls")}";
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
            // Play() lazily initialises LibVLC and returns early if unavailable.
            libVLCService.Play(Camera, false);

            if (!libVLCService.IsAvailable)
            {
                logger.Warn("Cannot apply camera controls because LibVLC is not available.");
                return;
            }

            logger.Info("Apply camera controls buton clicked");
        }
    }
}