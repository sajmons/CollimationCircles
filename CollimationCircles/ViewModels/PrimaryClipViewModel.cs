using Avalonia.Media;
using CollimationCircles.Models;
using CollimationCircles.Resources.Strings;

namespace CollimationCircles.ViewModels
{
    public partial class PrimaryClipViewModel : CollimationHelper
    {
        public PrimaryClipViewModel()
        {
            ItemColor = Colors.Lime;
            Label = Text.PrimaryClip;
            Radius = 268;
            Size = 50;   
            IsRotatable = true;
            IsSizeable = true;
            IsEditable = false;
            IsCountable = true;
            Count = 3;
        }
    }
}
