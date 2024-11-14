using Avalonia.Media;
using CollimationCircles.Models;
using CollimationCirclesFeatures;

namespace CollimationCircles.ViewModels
{
    public partial class SpiderViewModel : CollimationHelper
    {
        public SpiderViewModel()
        {
            ItemColor = Colors.Red;
            Label = ResSvc.TryGetString("Spider");
            Radius = 250;
            RotationAngle = 45;
            Size = 5;
            IsRotatable = true;
            IsInclinatable = false;
            IsSizeable = true;
            IsEditable = true;
            IsCountable = true;
            Count = 4;
        }
    }
}
