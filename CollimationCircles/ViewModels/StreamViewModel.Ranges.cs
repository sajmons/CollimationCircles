using CollimationCircles.Helper;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollimationCircles.ViewModels
{
    public partial class StreamViewModel
    {
        [ObservableProperty]
        private int cameraStreamTimeoutMin = Ranges.CameraStreamTimeoutMin;

        [ObservableProperty]
        private int cameraStreamTimeoutMax = Ranges.CameraStreamTimeoutMax;
    }
}
