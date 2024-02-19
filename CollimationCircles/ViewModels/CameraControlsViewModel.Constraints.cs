using CollimationCircles.Helper;

namespace CollimationCircles.ViewModels
{
    public partial class CameraControlsViewModel
    {
        public int BrightnessMin => Constraints.BrightnessMin;
        public int BrightnessMax => Constraints.BrightnessMax;
        
        public int SaturationMin => Constraints.SaturationMin;        
        public int SaturationMax => Constraints.SaturationMax;

        public int ContrastMin => Constraints.ContrastMin;
        public int ContrastMax => Constraints.ContrastMax;

        public int HueMin => Constraints.HueMin;
        public int HueMax => Constraints.HueMax;

        public int GammaMin => Constraints.GammaMin;
        public int GammaMax => Constraints.GammaMax;

        public int GainMin => Constraints.GainMin;
        public int GainMax => Constraints.GainMax;

        public int ZoomMin => Constraints.ZoomMin;
        public int ZoomMax => Constraints.ZoomMax;

        public int SharpnessMin => Constraints.SharpnessMin;
        public int SharpnessMax => Constraints.SharpnessMax;

        public int FocusMin => Constraints.FocusMin;
        public int FocusMax => Constraints.FocusMax;

        public int ExposureTimeMin => Constraints.ExposureTimeMin;
        public int ExposureTimeMax => Constraints.ExposureTimeMax;

        public int TemperatureMin => Constraints.TemperatureMin;
        public int TemperatureMax => Constraints.TemperatureMax;
    }
}
