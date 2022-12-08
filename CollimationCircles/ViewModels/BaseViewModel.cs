using CommunityToolkit.Mvvm.ComponentModel;

namespace CollimationCircles.ViewModels
{
    public partial class BaseViewModel : ObservableValidator
    {
        [ObservableProperty]        
        private string title = "Title";
    }
}
