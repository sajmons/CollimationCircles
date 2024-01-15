using Avalonia;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollimationCircles.Helper
{
    internal class Ranges
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

        internal const string MurlRegEx = "^(?:(?<protocol>tcp\\/h264|http|https):\\/\\/)?(?<host>[\\w\\.-]+)(?::(?<port>\\d+))?(?<path>\\/\\S*)?$";
    }
}
