using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace CollimationCircles.ViewModels
{
    public partial class CollimationCrossViewModel : BaseViewModel
    {
        [ObservableProperty]
        public IBrush brush = Brushes.Red;
        [ObservableProperty]
        [Range(1, 10)]
        public int thickness = 1;
        [ObservableProperty]
        [Range(1, 30)]
        public double spacing = 10;
        [ObservableProperty]
        [Range(1, 2000)]
        public double size = 300;
        [ObservableProperty]
        [Range(0, 90)]
        public double rotation = 45;
    }
}
