using Avalonia.Media;
using CollimationCircles.Models;
using CollimationCircles.Resources.Strings;

namespace CollimationCircles.ViewModels
{
    public partial class SpiderViewModel : CollimationHelper
    {
        public SpiderViewModel()
        {
            ItemColor = Colors.Red;
            Label = Text.Spider;
            Radius = 250;
            RotationAngle = 45;
            Size = 5;
            IsRotatable = true;
            IsSizeable = true;
            IsEditable = true;
            IsCountable = true;
            Count = 4;
        }
    }
}
