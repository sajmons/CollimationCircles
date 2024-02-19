using CollimationCircles.Helper;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp;
using System;
using System.ComponentModel.DataAnnotations;

namespace CollimationCircles.ViewModels
{
    public partial class CameraControlsViewModel : BaseViewModel
    {
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
        private bool autofocus;

        [ObservableProperty]
        [Range(Constraints.FocusMin, Constraints.FocusMax)]
        private int focus = -1;

        [ObservableProperty]
        private bool autoWhiteBalance;

        [ObservableProperty]
        [Range(Constraints.TemperatureMin, Constraints.TemperatureMax)]
        private int temperature = 5000;

        [ObservableProperty]
        [Range(Constraints.SharpnessMin, Constraints.SharpnessMax)]
        private int sharpness = 35;

        // camera controls

        [ObservableProperty]
        private bool autoExposure;

        [ObservableProperty]
        [Range(Constraints.ExposureTimeMin, Constraints.ExposureTimeMax)]
        private int exposureTime = 312;        

        [ObservableProperty]
        [Range(Constraints.ZoomMin, Constraints.ZoomMax)]
        private int zoom = 1;

        [ObservableProperty]
        private bool isOpened = false;        

        public CameraControlsViewModel(ICameraControlService cameraControlService)
        {
            this.cameraControlService = cameraControlService;
            this.cameraControlService.OnOpened += (sender, e) => IsOpened = true;
            this.cameraControlService.OnClosed += (sender, e) => IsOpened = false;            
        }

        partial void OnBrightnessChanged(int value)
        {
            cameraControlService.Brightness = value;
        }

        partial void OnSaturationChanged(int value)
        {
            cameraControlService.Saturation = value;
        }

        partial void OnContrastChanged(int value)
        {
            cameraControlService.Contrast = value;
        }

        partial void OnHueChanged(int value)
        {
            cameraControlService.Hue = value;
        }

        partial void OnGainChanged(int value)
        {
            cameraControlService.Gain = value;
        }

        partial void OnAutofocusChanged(bool value)
        {
            cameraControlService.Autofocus = value;
        }

        partial void OnFocusChanged(int value)
        {
            cameraControlService.Focus = value;
        }

        partial void OnZoomChanged(int value)
        {
            cameraControlService.Zoom = value;
        }

        partial void OnGammaChanged(int value)
        {
            cameraControlService.Gamma = value;
        }

        partial void OnSharpnessChanged(int value)
        {
            cameraControlService.Sharpness = value;
        }

        partial void OnExposureTimeChanged(int value)
        {
            cameraControlService.ExposureTime = value;
        }        

        partial void OnAutoExposureChanged(bool value)
        {
            cameraControlService.AutoExposure = value;
        }

        partial void OnTemperatureChanged(int value)
        {
            cameraControlService.Temperature = value;
        }
    }
}