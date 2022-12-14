using CollimationCircles.Models;
using CollimationCircles.Resources.Strings;

namespace CollimationCircles.ViewModels
{
    public partial class ScrewViewModel : CollimationHelper
    {
        public ScrewViewModel()
        {
            Color = ItemColor.Lime;
            Label = Text.PrimaryScrew;
            Radius = 270;
            IsRotatable = true;
            IsSizeable= true;
            IsEditable= false;
            IsCountable= true;            
        }
    }
}
