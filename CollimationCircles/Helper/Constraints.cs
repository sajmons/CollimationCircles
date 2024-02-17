namespace CollimationCircles.Helper
{
    internal class Constraints
    {
        internal const int ThicknessMin = 1;
        internal const int ThicknessMax = 10;

        internal const int RadiusMin = 1;
        internal const int RadiusMax = 2000;

        internal const int RotationAngleMin = -180;
        internal const int RotationAngleMax = 180;

        internal const int InclinationAngleMin = -90;
        internal const int InclinationAngleMax = 90;

        internal const int SizeMin = 1;
        internal const int SizeMax = 100;

        internal const int SpacingMin = 1;
        internal const int SpacingMax = 100;

        internal const int CountMin = 1;
        internal const int CountMax = 10;

        internal const double OpacityMin = 0.1;
        internal const double OpacityMax = 1;

        internal const double ScaleMin = 1;
        internal const double ScaleMax = 4;

        internal const int OffsetMin = -1000;
        internal const int OffsetMax = 1000;

        internal const int CameraStreamTimeoutMin = 100;
        internal const int CameraStreamTimeoutMax = 5000;

        internal const int LabelSizeMin = 5;
        internal const int LabelSizeMax = 1000;

        internal const string MurlRegEx = "^(?:(?<protocol>tcp\\/h264|http|https|dshow|v4l2|qtcapture):\\/\\/)?(?<host>[\\w\\.-]+)?(?::(?<port>\\d+))?(?<path>\\/\\S*)?$";

        // BRIGHTNESS,CONTRAST,SATURATION,HUE,GAIN,EXPOSURE,FOCUS,AUTOFOCUS,AUTO_EXPOSURE

        internal const double BrightnessMin = -65;
        internal const double BrightnessMax = 65;        

        internal const double ContrastMin = -65;
        internal const double ContrastMax = 65;

        internal const double SaturationMin = 0;
        internal const double SaturationMax = 255;

        internal const double HueMin = -65;
        internal const double HueMax = 65;

        internal const double GainMin = -65;
        internal const double GainMax = 65;

        internal const double FocusMin = -255;
        internal const double FocusMax = 255;

        internal const double GammaMin = 0;
        internal const double GammaMax = 255;

        internal const double SharpnessMin = 0;
        internal const double SharpnessMax = 128;

        internal const double ZoomMin = 0;
        internal const double ZoomMax = 2;
    }
}
