using CollimationCircles.Helper;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollimationCircles.ViewModels
{
    public partial class CameraControlsViewModel
    {
        public double BrightnessMin => Constraints.BrightnessMin;
        public double BrightnessMax => Constraints.BrightnessMax;
        
        public double SaturationMin => Constraints.SaturationMin;        
        public double SaturationMax => Constraints.SaturationMax;

        public double ContrastMin => Constraints.ContrastMin;
        public double ContrastMax => Constraints.ContrastMax;

        public double HueMin => Constraints.HueMin;
        public double HueMax => Constraints.HueMax;

        public double GammaMin => Constraints.GammaMin;
        public double GammaMax => Constraints.GammaMax;

        public double GainMin => Constraints.GainMin;
        public double GainMax => Constraints.GainMax;

        public double ZoomMin => Constraints.ZoomMin;
        public double ZoomMax => Constraints.ZoomMax;

        public double SharpnessMin => Constraints.SharpnessMin;
        public double SharpnessMax => Constraints.SharpnessMax;

        public double FocusMin => Constraints.FocusMin;
        public double FocusMax => Constraints.FocusMax;
    }
}
