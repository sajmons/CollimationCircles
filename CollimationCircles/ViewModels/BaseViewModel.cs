using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using System.Threading.Tasks;
using System;
using HanumanInstitute.MvvmDialogs;
using Newtonsoft.Json;

namespace CollimationCircles.ViewModels
{
    public partial class BaseViewModel : ObservableValidator
    {
        [ObservableProperty]
        public string title = string.Empty;

        [ObservableProperty]
        public string mainTitle = string.Empty;

        [JsonProperty]
        [ObservableProperty]
        private bool alwaysOnTop = true;

        internal readonly IResourceService ResSvc;
        internal readonly IDialogService DialogService;
        internal readonly ILicenseService LicenseService;

        private bool oldAllwysOnTop = false;

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

        public void DissableAlwaysOnTop()
        {
            oldAllwysOnTop = AlwaysOnTop;
            AlwaysOnTop = false;
        }

        public void RestoreAlwaysOnTop()
        {
            AlwaysOnTop = oldAllwysOnTop;
        }

        public void CheckFeatureLicensed(string feature, Action callback)
        {
            if (LicenseService.IsFeatureLicensed(feature))
            {
                callback?.Invoke();
            }
            else
            {
                DissableAlwaysOnTop();      // prevent new version dialog to appear behind MainWindow

                Task.Run(async () =>
                {
                    await DialogService.ShowMessageBoxAsync(null,
                        $"Feature '{feature}' is not supported by your current licence.\nPlease upgrade your license.", "Insifficient license", MessageBoxButton.Ok);
                });

                RestoreAlwaysOnTop();       // restore previous AlwaysOnTop setting
            }
        }

        public void CheckFeatureCount(string feature, int count, Action callback)
        {
            if (LicenseService.IsFeatureCount(feature, count))
            {
                callback?.Invoke();
            }
            else
            {
                DissableAlwaysOnTop();      // prevent new version dialog to appear behind MainWindow

                Task.Run(async () =>
                {
                    await DialogService.ShowMessageBoxAsync(null,
                    $"Feature '{feature}' count of '{count}' is not supported by your current licence.\nPlease upgrade your license.", "Insifficient license", MessageBoxButton.Ok);
                });

                RestoreAlwaysOnTop();       // restore previous AlwaysOnTop setting
            }
        }
    }
}
