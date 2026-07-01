using CollimationCircles.Messages;
using CollimationCircles.Models;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LibVLCSharp.Shared;
using System.ComponentModel;

namespace CollimationCircles.ViewModels
{
    public partial class CameraControlsViewModel : BaseViewModel
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly ILibVLCService libVLCService;
        private readonly StreamViewModel streamViewModel;

        [ObservableProperty]
        private Camera camera = new();

        [ObservableProperty]
        private bool isLibCamera;

        [ObservableProperty]
        private bool isPlaying;

        [ObservableProperty]
        private bool hasCameraControls;

        public CameraControlsViewModel()
        {
            this.libVLCService = Ioc.Default.GetRequiredService<ILibVLCService>();
            this.streamViewModel = Ioc.Default.GetRequiredService<StreamViewModel>();

            RefreshCameraContext();

            this.streamViewModel.PropertyChanged += StreamViewModel_PropertyChanged;

            WeakReferenceMessenger.Default.Register<CameraStateMessage>(this, (r, m) =>
            {
                IsPlaying = m.Value == CameraState.Playing;
                RefreshCameraContext();
            });

            Title = $"{ResSvc.TryGetString("CollimationCircles")} - {ResSvc.TryGetString("CameraControls")}";
        }

        private void StreamViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StreamViewModel.SelectedCamera) ||
                e.PropertyName == nameof(StreamViewModel.IsPlaying))
            {
                RefreshCameraContext();
            }
        }

        private void RefreshCameraContext()
        {
            Camera = streamViewModel.SelectedCamera;
            IsLibCamera = Camera.APIType == APIType.LibCamera;
            IsPlaying = streamViewModel.IsPlaying;
            HasCameraControls = Camera.Controls is not null && Camera.Controls.Count > 0;
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