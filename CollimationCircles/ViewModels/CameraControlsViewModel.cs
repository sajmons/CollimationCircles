using CollimationCircles.Helper;
using CollimationCircles.Messages;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using OpenCvSharp;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace CollimationCircles.ViewModels
{
    public partial class CameraControlsViewModel : BaseViewModel
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        readonly ICameraControlService cameraControlService;
        readonly ILibVLCService libVLCService;

        // user controls

        [ObservableProperty]
        [Range(Constraints.BrightnessMin, Constraints.BrightnessMax)]
        private int brightness = Constraints.BrightnessDefault;

        [ObservableProperty]
        [Range(Constraints.ContrastMin, Constraints.ContrastMax)]
        private int contrast = Constraints.ContrastDefault;

        [ObservableProperty]
        [Range(Constraints.SaturationMin, Constraints.SaturationMax)]
        private int saturation = Constraints.SaturationDefault;

        [ObservableProperty]
        [Range(Constraints.HueMin, Constraints.HueMax)]
        private int hue = Constraints.HueDefault;

        [ObservableProperty]
        [Range(Constraints.GammaMin, Constraints.GammaMax)]
        private int gamma = Constraints.GammaDefault;

        [ObservableProperty]
        [Range(Constraints.GainMin, Constraints.GainMax)]
        private int gain = Constraints.GainDefault;

        [ObservableProperty]
        private bool autoFocus = Constraints.AutoFocusDefault;

        [ObservableProperty]
        [Range(Constraints.FocusMin, Constraints.FocusMax)]
        private int focus = Constraints.FocusDefault;

        [ObservableProperty]
        private bool autoWhiteBalance = Constraints.AutoWhiteBalanceDefault;

        [ObservableProperty]
        [Range(Constraints.TemperatureMin, Constraints.TemperatureMax)]
        private int temperature = Constraints.TemperatureDefault;

        [ObservableProperty]
        [Range(Constraints.SharpnessMin, Constraints.SharpnessMax)]
        private int sharpness = Constraints.SharpnessDefault;

        // camera controls

        [ObservableProperty]
        private bool autoExposure = Constraints.AutoExposureTimeDefault;

        [ObservableProperty]
        [Range(Constraints.ExposureTimeMin, Constraints.ExposureTimeMax)]
        private int exposureTime = Constraints.ExposureTimeDefault;

        [ObservableProperty]
        [Range(Constraints.ZoomMin, Constraints.ZoomMax)]
        private int zoom = Constraints.ZoomDefault;

        [ObservableProperty]
        private bool isOpened = true;

        public CameraControlsViewModel(ICameraControlService cameraControlService, ILibVLCService libVLCService)
        {
            this.cameraControlService = cameraControlService;
            this.libVLCService = libVLCService;
            
            WeakReferenceMessenger.Default.Register<CameraStateMessage>(this, (r, m) =>
            {
                IsOpened = m.Value != CameraState.Stopped;
            });

            Title = $"{DynRes.TryGetString("CollimationCircles")} - {DynRes.TryGetString("CameraControls")}";
        }
        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            switch (e.PropertyName)
            {
                case nameof(Brightness):
                case nameof(Contrast):
                case nameof(Saturation):
                case nameof(Hue):
                case nameof(Gamma):
                case nameof(Gain):
                case nameof(AutoFocus):
                case nameof(Focus):
                case nameof(AutoWhiteBalance):
                case nameof(Temperature):
                case nameof(Sharpness):
                case nameof(AutoExposure):
                case nameof(ExposureTime):
                case nameof(Zoom):
                    if (!HasErrors)
                    {
                        base.OnPropertyChanged(e);

                        var pVal = Property.GetPropValue(this, e.PropertyName);

                        if (double.TryParse(pVal, out double valDouble))
                        {
                            cameraControlService.Set(e.PropertyName, valDouble, libVLCService.StreamSource);
                        }

                        logger.Debug($"{e.PropertyName} changed to '{pVal}'");
                    }
                    break;
            }
        }
        
        [RelayCommand]
        private void Default()
        {
            Brightness = Constraints.BrightnessDefault;
            Contrast = Constraints.ContrastDefault;
            Saturation = Constraints.SaturationDefault;
            Hue = Constraints.HueDefault;
            Gamma = Constraints.GammaDefault;
            Gain = Constraints.GainDefault;
            AutoFocus = Constraints.AutoFocusDefault;
            Focus = Constraints.FocusDefault;
            AutoWhiteBalance = Constraints.AutoWhiteBalanceDefault;
            Temperature = Constraints.TemperatureDefault;
            Sharpness = Constraints.SharpnessDefault;
            AutoExposure = Constraints.AutoExposureTimeDefault;
            ExposureTime = Constraints.ExposureTimeDefault;
            Zoom = Constraints.ZoomDefault;
        }
    }
}