using CollimationCircles.Helper;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace CollimationCircles.Models
{
    public partial class CameraControl : ObservableObject
    {
        private readonly ICameraControlService cameraControlService;
        private readonly ILibVLCService libVLCService;
        public ControlType Name { get; set; }
        public int Min { get; set; }
        public int Max { get; set; }
        public int Step { get; set; } = 1;
        public int Default { get; set; }

        [ObservableProperty]
        private int value;
        public string Flags { get; set; } = string.Empty;

        public CameraControl(ControlType controlName)
        {
            cameraControlService = Ioc.Default.GetRequiredService<ICameraControlService>();
            libVLCService = Ioc.Default.GetRequiredService<ILibVLCService>();
            Name = controlName;
            Initialize(controlName);
        }

        private void Initialize(ControlType controlName)
        {
            switch (controlName)
            {
                case ControlType.Brightness:
                    Value = Constraints.BrightnessDefault;
                    Default = Constraints.BrightnessDefault;
                    Min = Constraints.BrightnessMin;
                    Max = Constraints.BrightnessMax;
                    break;
                case ControlType.Contrast:
                    Value = Constraints.ContrastDefault;
                    Default = Constraints.ContrastDefault;
                    Min = Constraints.ContrastMin;
                    Max = Constraints.ContrastMax;
                    break;
                case ControlType.Saturation:
                    Value = Constraints.SaturationDefault;
                    Default = Constraints.SaturationDefault;
                    Min = Constraints.SaturationMin;
                    Max = Constraints.SaturationMax;
                    break;
                case ControlType.Hue:
                    Value = Constraints.HueDefault;
                    Default = Constraints.HueDefault;
                    Min = Constraints.HueMin;
                    Max = Constraints.HueMax;
                    break;
                case ControlType.Gain:
                    Value = Constraints.GainDefault;
                    Default = Constraints.GainDefault;
                    Min = Constraints.GainMin;
                    Max = Constraints.GainMax;
                    break;
                case ControlType.ExposureTime:
                    Value = Constraints.ExposureTimeDefault;
                    Default = Constraints.ExposureTimeDefault;
                    Min = Constraints.ExposureTimeMin;
                    Max = Constraints.ExposureTimeMax;
                    break;
                case ControlType.Sharpness:
                    Value = Constraints.SharpnessDefault;
                    Default = Constraints.SharpnessDefault;
                    Min = Constraints.SharpnessMin;
                    Max = Constraints.SharpnessMax;
                    break;
                case ControlType.Gamma:
                    Value = Constraints.GammaDefault;
                    Default = Constraints.GammaDefault;
                    Min = Constraints.GammaMin;
                    Max = Constraints.GammaMax;
                    break;
                case ControlType.Temperature:
                    Value = Constraints.TemperatureDefault;
                    Default = Constraints.TemperatureDefault;
                    Min = Constraints.TemperatureMin;
                    Max = Constraints.TemperatureMax;
                    break;
                //case ControlType.AutoWhiteBalance:
                //    Default = Constraints.AutoWhiteBalanceDefault ? 1 : 0;
                //    break;                
                case ControlType.Zoom:
                    Value = Constraints.ZoomDefault;
                    Default = Constraints.ZoomDefault;
                    Min = Constraints.ZoomMin;
                    Max = Constraints.ZoomMax;
                    break;
                case ControlType.Focus:
                    Value = Constraints.FocusDefault;
                    Default = Constraints.FocusDefault;
                    Min = Constraints.FocusMin;
                    Max = Constraints.FocusMax;
                    break;
                //case ControlType.AutoFocus:
                //    Default = Constraints.AutoFocusDefault ? 1 : 0;
                //    break;
                //case ControlType.AutoExposure:
                //    Default = Constraints.AutoExposureTimeDefault ? 1 : 0;
                //    break;
            }
        }

        partial void OnValueChanged(int oldValue, int newValue)
        {
            cameraControlService.Set(Name, newValue, libVLCService.Camera);
        }

        internal void SetDefault()
        {
            Initialize(Name);
        }
    }
}
