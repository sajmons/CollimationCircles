using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
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
        private bool isStandardLicense = true;

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

            try
            {
                ClipboardService.SetText(licenceJson);
            }
            catch (Exception ex)
            {
                string msg = string.Empty;

                if (!OperatingSystem.IsWindows())
                {
                    msg = "Please check if you have xsel installed and if not please run 'sudo apt install xsel' to install it.";
                }

                logger.Warn(ex, $"Error while copying licence to clipboard. {msg}");
            }

            AppService.OpenUrl(AppService.RequestLicensePage);

            if (dialog != null)
            {
                DialogService.Close(dialog);
                logger.Info("Request licence dialog closed");
            }
        }
    }
}