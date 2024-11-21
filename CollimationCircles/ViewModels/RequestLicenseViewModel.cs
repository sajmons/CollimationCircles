using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.ComponentModel;
using TextCopy;

namespace CollimationCircles.ViewModels
{
    internal partial class RequestLicenseViewModel : BaseViewModel
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private INotifyPropertyChanged? dialog;

        [ObservableProperty]
        private string license;

        [ObservableProperty]
        private string clientId;

        [ObservableProperty]
        private string product = AppService.ProductName;

        [ObservableProperty]
        private string productMajorVersion = AppService.GetAppMajorVersion();

        [ObservableProperty]
        private bool isStandardLicense = true;

        public bool ShowRequestLicenceButton => !LicenseService.IsValid || (LicenseService.HasLicense && LicenseService.IsExpired);

        public RequestLicenseViewModel()
        {
            License = $"{LicenseService}";
            ClientId = libc.hwid.HwId.Generate();
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

            DissableAlwaysOnTop();
            DialogService.Show(null, dialog);
            logger.Info("Request licence dialog opened");
            RestoreAlwaysOnTop();
        }

        [RelayCommand]
        internal void Submit()
        {
            logger.Info("Licence request submited");

            // submit licence to author
            var a = new
            {
                ClientId,
                Product,
                ProductMajorVersion,
                IsStandardLicense
            };

            string payPalTransactionId = ResSvc.TryGetString("PayPalTransactionId");

            string licenceJson = $"{AppService.Serialize(a)}{Environment.NewLine}{payPalTransactionId}";

            ClipboardService.SetText(licenceJson);

            AppService.OpenUrl(AppService.RequestLicensePage);

            if (dialog != null)
            {
                DialogService.Close(dialog);
                logger.Info("Request licence dialog closed");
            }
        }
    }
}