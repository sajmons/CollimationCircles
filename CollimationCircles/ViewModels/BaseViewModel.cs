using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;

namespace CollimationCircles.ViewModels
{
    public partial class BaseViewModel : ObservableValidator
    {
        [ObservableProperty]
        public string title = string.Empty;

        [ObservableProperty]
        public string mainTitle = string.Empty;

        internal IResourceService ResSvc { get; set; }

        public BaseViewModel()
        {
            ResSvc = Ioc.Default.GetRequiredService<IResourceService>();
        }

        [RelayCommand]
        public void Translate(string targetLanguage)
        {            
            ResSvc.Translate(targetLanguage);
        }
    }
}
