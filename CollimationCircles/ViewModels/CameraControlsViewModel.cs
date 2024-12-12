using CollimationCircles.Messages;
using CollimationCircles.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace CollimationCircles.ViewModels
{
    public partial class CameraControlsViewModel : BaseViewModel
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        [ObservableProperty]
        private bool isOpened = true;

        [ObservableProperty]
        private Camera? camera;

        public CameraControlsViewModel()
        {            
            camera = Ioc.Default.GetRequiredService<StreamViewModel>().SelectedCamera;

            WeakReferenceMessenger.Default.Register<CameraStateMessage>(this, (r, m) =>
            {
                IsOpened = m.Value != CameraState.Stopped;
            });

            Title = $"{ResSvc.TryGetString("CollimationCircles")} - {ResSvc.TryGetString("CameraControls")}";
        }

        [RelayCommand]
        private void Default()
        {
            Camera?.SetDefaultControls();
            logger.Info("Default camera controls buton clicked");
        }
    }
}