using Avalonia.Media;
using CollimationCircles.Models;
using CollimationCircles.Resources.Strings;

namespace CollimationCircles.ViewModels
{
    public partial class CrossViewModel : CollimationHelper
    {
        public CrossViewModel()
        {
            RotationAngle = 45;
            Size = 4;
            ItemColor = Colors.Red;
            Label = Text.Cross;
            Radius= 250;
            IsRotatable= true;
            IsSizeable= true;
        }
    }
}
