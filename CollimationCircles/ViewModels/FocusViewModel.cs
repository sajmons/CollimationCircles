using Avalonia.Media;
using CollimationCircles.Models;
using CollimationCircles.Resources.Strings;

namespace CollimationCircles.ViewModels
{
    public partial class FocusViewModel : CollimationHelper
    {
        public FocusViewModel()
        {
            ItemColor = Colors.Lime;
            Label = Text.PrimaryScrew;
            Radius = 230;
            IsRotatable = true;
            IsInclinatable = true;
            IsSizeable = true;
            IsEditable = true;
            IsCountable = true;
            Count = 3;
            RotationAngle = 60;
            InclinationAngle = 10;
        }
    }
}
