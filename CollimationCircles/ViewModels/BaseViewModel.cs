using Avalonia.Threading;
using CollimationCircles.Extensions;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;

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

        [ObservableProperty]
        private bool invalidLicense = true;

        [ObservableProperty]
        private bool validLicense = false;

        [ObservableProperty]
        private string? license;

        [ObservableProperty]
        private string clientId;

        [ObservableProperty]
        private string product = AppService.ProductName;

        [ObservableProperty]
        private string productMajorVersion = AppService.GetAppMajorVersion();

        internal readonly IResourceService ResSvc;
        internal readonly IDialogService DialogService;
        internal readonly ILicenseService LicenseService;

        public BaseViewModel()
        {
            ResSvc = Ioc.Default.GetRequiredService<IResourceService>();
            DialogService = Ioc.Default.GetRequiredService<IDialogService>(); ;
            LicenseService = Ioc.Default.GetRequiredService<ILicenseService>();

            ClientId = AppService.DeviceId();

            InvalidLicense = !LicenseService.IsValid || (!LicenseService.HasLicense && !LicenseService.IsExpired);
            ValidLicense = !InvalidLicense;

            Initialize();
        }

        protected virtual void Initialize()
        {
            License = $"{LicenseService}";
            Title = $"{ResSvc.TryGetString("CollimationCircles")} - {ResSvc.TryGetString("Version")} {AppService.GetAppVersionTitle()} {LicenseService}";
        }

        [RelayCommand]
        public void Translate(string targetLanguage)
        {
            ResSvc.Translate(targetLanguage);
            Initialize();
        }

        public void InCaseOfValidLicense(Action callback)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                await InCaseOfValidLicenseAsync(callback);
            });
        }

        public async Task InCaseOfValidLicenseAsync(Action callback)
        {
            if (LicenseService.IsValid)
            {
                callback?.Invoke();
            }
            else
            {
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

                if (dialogResult == true)
                {
                    // user requested licence
                    var rlVm = Ioc.Default.GetRequiredService<RequestLicenseViewModel>();

                    rlVm.RequestLicense();
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

                if (dialogResult == true)
                {
                    // user requested licence
                    var rlVm = Ioc.Default.GetRequiredService<RequestLicenseViewModel>();

                    rlVm.RequestLicense();
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

                if (dialogResult == true)
                {
                    // user requested licence
                    var rlVm = Ioc.Default.GetRequiredService<RequestLicenseViewModel>();

                    rlVm.RequestLicense();
                }
            }
        }
    }
}
