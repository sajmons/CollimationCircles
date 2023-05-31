using Avalonia.Media;
using CollimationCircles.Models;
using CollimationCircles.Resources.Strings;

namespace CollimationCircles.ViewModels
{
    public partial class PrimaryClipViewModel : CollimationHelper
    {
        public PrimaryClipViewModel()
        {
            ItemColor = Colors.White;
            Label = Text.PrimaryClip;
            Radius = 268;
            Size = 50;
            IsRotatable = true;
            IsInclinatable = false;
            IsSizeable = true;
            IsEditable = true;
            IsCountable = true;
            Count = 3;
            RotationAngle = 30;
        }
    }
}
