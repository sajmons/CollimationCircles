using Avalonia.Media;
using CollimationCircles.Helper;
using CollimationCircles.Models;
using CollimationCircles.Services;

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
