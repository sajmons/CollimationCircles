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
        private string licenseRequestText = string.Empty;        

        public RequestLicenseViewModel()
        {
            Title = $"{ResSvc.TryGetString("RequestLicense")} - {ResSvc.TryGetString("CollimationCircles")} - {ResSvc.TryGetString("Version")} {AppService.GetAppVersionTitle()}";

            LicenseRequestText = ClientId;
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
        internal void BuyLicense()
        {
            AppService.OpenUrl(AppService.LicenseUrl);

            if (dialog != null)
            {
                ClipboardService.SetText(LicenseRequestText);
                logger.Info($"Buy licence requested");
                logger.Info($"License data requested: {LicenseRequestText}");
            }
        }        
    }
}