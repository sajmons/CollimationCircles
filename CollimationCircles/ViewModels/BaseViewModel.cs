using CommunityToolkit.Mvvm.ComponentModel;

namespace CollimationCircles.ViewModels
{
    public partial class BaseViewModel : ObservableValidator
    {
        [ObservableProperty]
        public string mainTitle = string.Empty;

        [ObservableProperty]
        public string title = string.Empty;
    }
}
