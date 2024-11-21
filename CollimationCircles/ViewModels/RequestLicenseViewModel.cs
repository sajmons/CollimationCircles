using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using System.ComponentModel;

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
        [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
        private string? customer;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
        private string? customerEmail;

        [ObservableProperty]
        private bool isTrialSelected = false;

        [ObservableProperty]
        private decimal licenseCost;        

        public bool CanExecuteSubmit
        {
            get => !string.IsNullOrWhiteSpace(Customer) && !string.IsNullOrWhiteSpace(CustomerEmail);
        }

        public bool ShowRequestLicenceButton => !LicenseService.IsValid || (LicenseService.HasLicense && LicenseService.IsExpired);

        public RequestLicenseViewModel()
        {
            License = $"{LicenseService}";
            ClientId = libc.hwid.HwId.Generate();
            Title = $"{ResSvc.TryGetString("RequestLicense")} - {ResSvc.TryGetString("CollimationCircles")} - {ResSvc.TryGetString("Version")} {AppService.GetAppVersionTitle()}";

            LicenseCost = IsTrialSelected ? 0.00M : 19.99M;
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

        [RelayCommand(CanExecute = nameof(CanExecuteSubmit))]
        internal void Submit()
        {
            logger.Info("Licence request submited");
            // submit licence to author

            if (dialog != null)
            {
                DialogService.Close(dialog);
                logger.Info("Request licence dialog closed");
            }
        }

        partial void OnIsTrialSelectedChanged(bool oldValue, bool newValue)
        {
            LicenseCost = IsTrialSelected ? 0.00M : 19.99M;
        }
    }
}