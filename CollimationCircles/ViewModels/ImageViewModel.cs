using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollimationCircles.ViewModels
{
    public partial class ImageViewModel : BaseViewModel
    {
        [ObservableProperty]
        private Bitmap? imageToDisplay;

        [ObservableProperty]
        private string? imageDescription;

        public ImageViewModel()
        {

        }
    }
}
