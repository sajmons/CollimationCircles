using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using System.Threading.Tasks;
using System;
using HanumanInstitute.MvvmDialogs;
using Newtonsoft.Json;
using CollimationCircles.Extensions;
using Tmds.DBus.Protocol;

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

        public async Task CheckValidLicense(Action callback)
        {
            if (LicenseService.IsValid)
            {
                callback?.Invoke();
            }
            else
            {
                DissableAlwaysOnTop();      // prevent dialog to appear behind MainWindow

                string title = ResSvc.TryGetString("InsifficientLicenseTitle");

                string message = ResSvc.TryGetString("InsifficientLicenseMessage");

                if (LicenseService.IsExpired)
                {
                    string expired = ResSvc.TryGetString("ExpiredLicenseMessage").F(LicenseService.Expiration!.Value.ToLongDateString());
                    message += Environment.NewLine + expired;
                }

                string upgrade = ResSvc.TryGetString("UpgradeLicenseMessage");

                string dialogMessage = $"{message}\n{upgrade}";

                var dialogResult = await DialogService.ShowMessageBoxAsync(null, dialogMessage, title, MessageBoxButton.YesNo);

                RestoreAlwaysOnTop();       // restore previous AlwaysOnTop setting

                if (dialogResult == true)
                {
                    // user requested licence
                }
            }
        }

        public async Task CheckFeatureLicensed(string feature, Action callback)
        {
            if (LicenseService.IsFeatureLicensed(feature))
            {
                callback?.Invoke();
            }
            else
            {
                DissableAlwaysOnTop();      // prevent dialog to appear behind MainWindow

                string title = ResSvc.TryGetString("InsifficientLicenseTitle");
                
                string message = ResSvc.TryGetString("InsifficientLicenseMessage").F(feature);

                if (LicenseService.IsExpired)
                {
                    string expired = ResSvc.TryGetString("ExpiredLicenseMessage").F(LicenseService.Expiration!.Value.ToLongDateString());
                    message += Environment.NewLine + expired;
                }

                string upgrade = ResSvc.TryGetString("UpgradeLicenseMessage");

                string dialogMessage = $"{message}\n{upgrade}";

                var dialogResult = await DialogService.ShowMessageBoxAsync(null, dialogMessage, title, MessageBoxButton.YesNo);

                RestoreAlwaysOnTop();       // restore previous AlwaysOnTop setting

                if (dialogResult == true)
                {
                    // user requested licence
                }
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
                DissableAlwaysOnTop();      // prevent dialog to appear behind MainWindow

                string title = ResSvc.TryGetString("InsifficientLicenseTitle");

                string message = ResSvc.TryGetString("InsifficientLicenseCountMessage").F(feature, count);

                if (LicenseService.IsExpired)
                {
                    string expired = ResSvc.TryGetString("ExpiredLicenseMessage").F(LicenseService.Expiration!.Value.ToLongDateString());
                    message += Environment.NewLine + expired;
                }

                string upgrade = ResSvc.TryGetString("UpgradeLicenseMessage");

                string dialogMessage = $"{message}\n{upgrade}";                

                var dialogResult = await DialogService.ShowMessageBoxAsync(null, dialogMessage, title, MessageBoxButton.YesNo);

                RestoreAlwaysOnTop();       // restore previous AlwaysOnTop setting

                if (dialogResult == true)
                {
                    // user requested licence
                }
            }
        }
    }
}
