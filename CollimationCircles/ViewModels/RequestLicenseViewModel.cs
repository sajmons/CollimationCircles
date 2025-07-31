using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
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

        [ObservableProperty]
        private bool isTrialLicense = false;

        [ObservableProperty]
        private string licenseRequestText = string.Empty;        

        private decimal licensePrice = 10.00m;
        private string licenseDuration = "Unlimited";

        public RequestLicenseViewModel()
        {
            Title = $"{ResSvc.TryGetString("RequestLicense")} - {ResSvc.TryGetString("CollimationCircles")} - {ResSvc.TryGetString("Version")} {AppService.GetAppVersionTitle()}";
            
            LicenseRequestText = GetLicenceText();
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
            if (IsStandardLicense)
            {
                AppService.OpenUrl(AppService.PatreonShop);
            }
            else
            {
                AppService.OpenUrl(AppService.RequestLicensePage);
            }

            if (dialog != null)
            {
                ClipboardService.SetText(LicenseRequestText);
                DialogService.Close(dialog);
                logger.Info($"Request licence dialog closed");
                logger.Info($"License data submited: {LicenseRequestText}");
            }
        }

        partial void OnIsStandardLicenseChanged(bool value)
        {
            IsStandardLicense = value;
            IsTrialLicense = !IsStandardLicense;

            licensePrice = IsStandardLicense ? 10.00m : 0.00m; // Set price based on license type
            licenseDuration = IsStandardLicense ? "Unlimited" : "30 days"; // Set duration based on license type

            LicenseRequestText = GetLicenceText();            
        }

        private string GetLicenceText()
        {
            // licence information
            string licenseType = IsStandardLicense ? "Standard" : "Trial";

            return $"ClientId: {ClientId}" +
                $"\nProduct: {Product} {ProductMajorVersion}" +
                $"\nLicense Type: {licenseType}" +
                $"\nPrice: {licensePrice:C}" +
                $"\nDuration: {licenseDuration}";
        }
    }
}