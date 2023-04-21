using Avalonia.Media;
using CollimationCircles.Models;
using CollimationCircles.Resources.Strings;

namespace CollimationCircles.ViewModels
{
    public partial class CircleViewModel : CollimationHelper
    {
        public CircleViewModel()
        {
            ItemColor = Colors.Red;
            Label = Text.Circle;
        }
    }
}
