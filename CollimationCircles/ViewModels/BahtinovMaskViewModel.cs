using Avalonia.Media;
using CollimationCircles.Models;

namespace CollimationCircles.ViewModels
{
    public partial class BahtinovMaskViewModel : CollimationHelper
    {
        public BahtinovMaskViewModel()
        {
            ItemColor = Colors.Blue;
            Label = ResSvc.TryGetString("BahtinovMask");
            Radius = 230;
            IsRotatable = true;
            IsInclinatable = true;
            IsSizeable = true;
            IsEditable = true;
            IsCountable = true;
            RotationAngle = 90;
            InclinationAngle = 10;
            RotationIncrement = 0.1;
            InclinationIncrement = 0.1;
            Count = 1;
            MaxCount = 3;
        }
    }
}
