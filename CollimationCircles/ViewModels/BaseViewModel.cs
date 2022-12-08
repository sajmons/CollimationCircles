using CommunityToolkit.Mvvm.ComponentModel;
using System.Text.Json.Serialization;

namespace CollimationCircles.ViewModels
{
    public partial class BaseViewModel : ObservableValidator
    {
        [ObservableProperty]
        [JsonIgnore]
        private string title = "Title";
    }
}
