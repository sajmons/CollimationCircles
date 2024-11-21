using CollimationCircles.Services;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using System.Web;

namespace CollimationCircles.ViewModels
{
    internal partial class AboutViewModel : BaseViewModel, IModalDialogViewModel
    {
        public bool? DialogResult => true;

        public AboutViewModel()
        {
            Title = $"{ResSvc.TryGetString("About")} - {ResSvc.TryGetString("CollimationCircles")} - {ResSvc.TryGetString("Version")} {AppService.GetAppVersionTitle()}";
        }

        [RelayCommand]
        internal static void OpenPatreonWebSite()
        {
            AppService.OpenUrl(AppService.PatreonWebPage);
        }

        [RelayCommand]
        internal void CloseDialog()
        {
            DialogService.Close(this);
        }

        [RelayCommand]
        internal void PayPalDonate()
        {
            string text = ResSvc.TryGetString("PayPalDonation");

            string encodedText = HttpUtility.UrlEncode(text);

            string url = $"https://www.paypal.com/donate/?business=DBUQU9W2LNS2G&no_recurring=0&item_name={encodedText}&currency_code=EUR";

            AppService.OpenUrl(url);
        }
    }
}