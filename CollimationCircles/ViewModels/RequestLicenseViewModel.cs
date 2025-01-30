using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using TextCopy;

namespace CollimationCircles.ViewModels
{
    internal partial class RequestLicenseViewModel : BaseViewModel
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private INotifyPropertyChanged? dialog;

        [ObservableProperty]
        private bool isStandardLicense = true;

        [ObservableProperty]
        private bool isTrialLicense = false;

        public RequestLicenseViewModel()
        {
            Title = $"{ResSvc.TryGetString("RequestLicense")} - {ResSvc.TryGetString("CollimationCircles")} - {ResSvc.TryGetString("Version")} {AppService.GetAppVersionTitle()}";
        }

        [RelayCommand]
        internal void CloseDialog()
        {
            DialogService.Close(this);
        }

        [RelayCommand]
        internal void RequestLicense()
        {
            dialog = DialogService.CreateViewModel<RequestLicenseViewModel>();
            var parent = Ioc.Default.GetRequiredService<SettingsViewModel>();

            DialogService.Show(parent, dialog);
            logger.Info("Request licence dialog opened");
        }

        [RelayCommand]
        internal async Task Submit()
        {
            logger.Info("Licence request submited");

            if (IsStandardLicense)
            {
                AppService.OpenUrl(AppService.PatreonShop);
            }
            else
            {
                // submit licence to author
                var a = new
                {
                    ClientId,
                    Product,
                    ProductMajorVersion
                };

                string licenceJson = $"{AppService.Serialize(a, Formatting.Indented)}";

                bool? result = await DialogService.ShowMessageBoxAsync(null,
                    $"Please select text below and copy it to your clipboard (CRTL+C).\nAfter clicking OK button you will be redirected to my web page {AppService.RequestLicensePage}.\n\n{licenceJson}",
                    "Buy licence", HanumanInstitute.MvvmDialogs.FrameworkDialogs.MessageBoxButton.OkCancel);

                if (result is true)
                {
                    AppService.OpenUrl(AppService.RequestLicensePage);
                }
            }

            if (dialog != null)
            {
                DialogService.Close(dialog);
                logger.Info("Request licence dialog closed");
            }
        }

        partial void OnIsStandardLicenseChanged(bool value)
        {
            IsStandardLicense = value;
            IsTrialLicense = !IsStandardLicense;
        }
    }
}