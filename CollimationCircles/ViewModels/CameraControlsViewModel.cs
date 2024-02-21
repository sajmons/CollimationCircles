using CollimationCircles.Helper;
using CollimationCircles.Messages;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
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

        // user controls

        [ObservableProperty]
        [Range(Constraints.BrightnessMin, Constraints.BrightnessMax)]
        private int brightness = 0;

        [ObservableProperty]
        [Range(Constraints.ContrastMin, Constraints.ContrastMax)]
        private int contrast = 13;

        [ObservableProperty]
        [Range(Constraints.SaturationMin, Constraints.SaturationMax)]
        private int saturation = 38;

        [ObservableProperty]
        [Range(Constraints.HueMin, Constraints.HueMax)]
        private int hue = 0;

        [ObservableProperty]
        [Range(Constraints.GammaMin, Constraints.GammaMax)]
        private int gamma = 100;

        [ObservableProperty]
        [Range(Constraints.GainMin, Constraints.GainMax)]
        private int gain = -1;

        [ObservableProperty]
        private bool autoFocus = true;

        [ObservableProperty]
        [Range(Constraints.FocusMin, Constraints.FocusMax)]
        private int focus = -1;

        [ObservableProperty]
        private bool autoWhiteBalance = true;

        [ObservableProperty]
        [Range(Constraints.TemperatureMin, Constraints.TemperatureMax)]
        private int temperature = 5000;

        [ObservableProperty]
        [Range(Constraints.SharpnessMin, Constraints.SharpnessMax)]
        private int sharpness = 35;

        // camera controls

        [ObservableProperty]
        private bool autoExposure = true;

        [ObservableProperty]
        [Range(Constraints.ExposureTimeMin, Constraints.ExposureTimeMax)]
        private int exposureTime = 312;

        [ObservableProperty]
        [Range(Constraints.ZoomMin, Constraints.ZoomMax)]
        private int zoom = 1;

        [ObservableProperty]
        private bool isOpened = true;

        public CameraControlsViewModel(ICameraControlService cameraControlService)
        {
            this.cameraControlService = cameraControlService;
            
            WeakReferenceMessenger.Default.Register<CameraStateMessage>(this, (r, m) =>
            {
                IsOpened = m.Value;
            });

            Title = $"{DynRes.TryGetString("CollimationCircles")} - {DynRes.TryGetString("CameraSettings")}";
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
                            cameraControlService.Set(e.PropertyName, valDouble);
                        }

                        logger.Debug($"{e.PropertyName} changed to '{pVal}'");
                    }
                    break;
            }
        }
    }
}