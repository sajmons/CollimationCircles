using Avalonia.Media;
using CollimationCircles.Models;
using CollimationCircles.Resources.Strings;

namespace CollimationCircles.ViewModels
{
    public partial class CrossViewModel : CollimationHelper
    {
        public CrossViewModel()
        {
            Rotation = 45;
            Size = 4;
            ItemColor = Colors.Red;
            Label = Text.Spider;
            Radius= 200;
            IsRotatable= true;
            IsSizeable= true;
        }
    }
}
