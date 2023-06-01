using Avalonia.Media;
using CollimationCircles.Models;
using CollimationCircles.Resources.Strings;

namespace CollimationCircles.ViewModels
{
    public partial class BahtinovMaskViewModel : CollimationHelper
    {
        public BahtinovMaskViewModel()
        {
            ItemColor = Colors.Blue;
            Label = Text.BahtinovMask;
            Radius = 230;
            IsRotatable = true;
            IsInclinatable = true;
            IsSizeable = true;
            IsEditable = true;
            IsCountable = false;
            RotationAngle = 90;
            InclinationAngle = 10;
            RotationIncrement = 0.1;
            InclinationIncrement = 0.1;
        }
    }
}
