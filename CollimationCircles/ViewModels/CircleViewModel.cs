using Avalonia.Media;
using CollimationCircles.Helper;
using CollimationCircles.Models;

namespace CollimationCircles.ViewModels
{
    public partial class CircleViewModel : CollimationHelper
    {
        public CircleViewModel()
        {
            ItemColor = Colors.Red;
            Label = DynRes.TryGetString("Circle");
        }
    }
}
