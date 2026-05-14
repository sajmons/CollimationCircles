using CollimationCircles.Messages;
using CollimationCircles.Models;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace CollimationCircles.ViewModels
{
    public partial class CameraControlsViewModel : BaseViewModel
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly ILibVLCService libVLCService;

        [ObservableProperty]
        private Camera camera;

        [ObservableProperty]
        private bool isLibCamera;

        public CameraControlsViewModel()
        {
            this.libVLCService = Ioc.Default.GetRequiredService<ILibVLCService>();
            
            Camera = Ioc.Default.GetRequiredService<StreamViewModel>().SelectedCamera;
            IsLibCamera = Camera.APIType == APIType.LibCamera;

            WeakReferenceMessenger.Default.Register<CameraStateMessage>(this, (r, m) =>
            {
                Camera = Ioc.Default.GetRequiredService<StreamViewModel>().SelectedCamera;
                IsLibCamera = Camera.APIType == APIType.LibCamera;
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
            libVLCService.Play(Camera, false);
            logger.Info("Apply camera controls buton clicked");
        }
    }
}