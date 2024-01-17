using CollimationCircles.Helper;
using CommunityToolkit.Mvvm.ComponentModel;

namespace CollimationCircles.ViewModels
{
    public partial class StreamViewModel
    {
        [ObservableProperty]
        private int cameraStreamTimeoutMin = Constraints.CameraStreamTimeoutMin;

        [ObservableProperty]
        private int cameraStreamTimeoutMax = Constraints.CameraStreamTimeoutMax;
    }
}
