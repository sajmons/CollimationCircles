using Avalonia.Media;
using CollimationCircles.Helper;
using CollimationCircles.Models;

namespace CollimationCircles.ViewModels
{
    public partial class SpiderViewModel : CollimationHelper
    {
        public SpiderViewModel()
        {
            ItemColor = Colors.Red;
            Label = DynRes.TryGetString("Spider");
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
