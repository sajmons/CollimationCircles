using CollimationCircles.Helper;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp;
using System.ComponentModel.DataAnnotations;

namespace CollimationCircles.ViewModels
{
    public partial class CameraControlsViewModel(ICameraControlService cameraControlService) : BaseViewModel
    {
        readonly ICameraControlService cameraControlService = cameraControlService;

        [ObservableProperty]
        [Range(Constraints.BrightnessMin, Constraints.BrightnessMax)]
        private double brightness = 0.0;

        [ObservableProperty]
        [Range(Constraints.ContrastMin, Constraints.ContrastMax)]
        private double contrast = 0.0;

        [ObservableProperty]
        [Range(Constraints.SaturationMin, Constraints.SaturationMax)]
        private double saturation;

        [ObservableProperty]
        [Range(Constraints.HueMin, Constraints.HueMax)]
        private double hue;

        [ObservableProperty]
        [Range(Constraints.GainMin, Constraints.GainMax)]
        private double gain;

        [ObservableProperty]
        private bool autofocus;

        [ObservableProperty]
        [Range(Constraints.FocusMin, Constraints.FocusMax)]
        private double focus;        

        [ObservableProperty]
        [Range(Constraints.GammaMin, Constraints.GammaMax)]
        private double gamma;

        [ObservableProperty]
        [Range(Constraints.SharpnessMin, Constraints.SharpnessMax)]
        private double sharpness;

        [ObservableProperty]
        [Range(Constraints.ZoomMin, Constraints.ZoomMax)]
        private double zoom;        

        partial void OnBrightnessChanged(double value)
        {
            cameraControlService.Brightness = value;
        }

        partial void OnSaturationChanged(double value)
        {
            cameraControlService.Saturation = value;
        }

        partial void OnContrastChanged(double value)
        {
            cameraControlService.Contrast = value;
        }

        partial void OnHueChanged(double value)
        {
            cameraControlService.Hue = value;
        }

        partial void OnGainChanged(double value)
        {
            cameraControlService.Gain = value;
        }

        partial void OnAutofocusChanged(bool value)
        {
            cameraControlService.Autofocus = value;
        }

        partial void OnFocusChanged(double value)
        {
            cameraControlService.Focus = value;
        }

        partial void OnGainChanging(double value)
        {
            cameraControlService.Gain = value;
        }

        partial void OnZoomChanged(double value)
        {
            cameraControlService.Zoom = value;
        }

        partial void OnGammaChanged(double value)
        {
            cameraControlService.Gamma = value;
        }

        partial void OnSharpnessChanged(double value)
        {
            cameraControlService.Sharpness = value;
        }        
    }
}