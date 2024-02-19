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

        internal const int BrightnessMin = -65;
        internal const int BrightnessMax = 65;        

        internal const int ContrastMin = -65;
        internal const int ContrastMax = 65;

        internal const int SaturationMin = 0;
        internal const int SaturationMax = 255;

        internal const int HueMin = -65;
        internal const int HueMax = 65;

        internal const int GainMin = -65;
        internal const int GainMax = 65;

        internal const int FocusMin = -255;
        internal const int FocusMax = 255;

        internal const int GammaMin = 0;
        internal const int GammaMax = 255;

        internal const int SharpnessMin = 0;
        internal const int SharpnessMax = 128;

        internal const int ZoomMin = 1;
        internal const int ZoomMax = 2;

        internal const double ExposureMin = 0.01;
        internal const double ExposureMax = 255;
    }
}
