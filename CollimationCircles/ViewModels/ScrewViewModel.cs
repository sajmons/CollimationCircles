using Avalonia.Media;
using CollimationCircles.Models;

namespace CollimationCircles.ViewModels
{
    public partial class ScrewViewModel : CollimationHelper
    {
        public ScrewViewModel()
        {
            ItemColor = Colors.Lime;
            Label = ResSvc.TryGetString("PrimaryScrew");
            Radius = 230;
            IsRotatable = true;
            IsInclinatable = false;
            IsSizeable = true;
            IsEditable = true;
            IsCountable = true;
            Count = 3;
            RotationAngle = 60;
        }
    }
}
