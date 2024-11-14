using Avalonia.Media;
using CollimationCircles.Models;
using CollimationCirclesFeatures;

namespace CollimationCircles.ViewModels
{
    public partial class CircleViewModel : CollimationHelper
    {
        public CircleViewModel()
        {
            ItemColor = Colors.Red;
            Label = ResSvc.TryGetString("Circle");
        }
    }
}
