using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using System.Threading.Tasks;
using System;
using HanumanInstitute.MvvmDialogs;

namespace CollimationCircles.ViewModels
{
    public partial class BaseViewModel : ObservableValidator
    {
        [ObservableProperty]
        public string title = string.Empty;

        [ObservableProperty]
        public string mainTitle = string.Empty;

        internal readonly IResourceService ResSvc;
        internal readonly IDialogService DialogService;
        internal readonly ILicenseService LicenseService;

        public BaseViewModel()
        {
            ResSvc = Ioc.Default.GetRequiredService<IResourceService>();            
            DialogService = Ioc.Default.GetRequiredService<IDialogService>(); ;
            LicenseService = Ioc.Default.GetRequiredService<ILicenseService>();
        }

        [RelayCommand]
        public void Translate(string targetLanguage)
        {
            ResSvc.Translate(targetLanguage);
        }

        public async Task CheckFeatureLicensed(string feature, Action callback)
        {
            if (LicenseService.IsFeatureLicensed(feature))
            {
                callback?.Invoke();
            }
            else
            {
                await DialogService.ShowMessageBoxAsync(null,
                    $"Feature '{feature}' is not supported by your current licence.\nPlease upgrade your license.", "Insifficient license", MessageBoxButton.Ok);
            }
        }

        public async Task CheckFeatureCount(string feature, int count, Action callback)
        {
            if (LicenseService.IsFeatureCount(feature, count))
            {
                callback?.Invoke();
            }
            else
            {
                await DialogService.ShowMessageBoxAsync(null,
                    $"Feature '{feature}' count of '{count}' is not supported by your current licence.\nPlease upgrade your license.", "Insifficient license", MessageBoxButton.Ok);
            }
        }
    }
}
