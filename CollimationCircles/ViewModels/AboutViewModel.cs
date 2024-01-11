using CollimationCircles.Helper;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using System;
using System.Web;

namespace CollimationCircles.ViewModels
{
    internal partial class AboutViewModel : BaseViewModel, IModalDialogViewModel
    {
        public bool? DialogResult => true;
        
        private readonly IDialogService dialogService;


        public AboutViewModel(IDialogService dialogService)
        {
            this.dialogService = dialogService;

            Title = $"{DynRes.TryGetString("About")} - {DynRes.TryGetString("CollimationCircles")} - {DynRes.TryGetString("Version")} {AppService.GetAppVersion()}";
        }

        [RelayCommand]
        internal static void OpenPatreonWebSite()
        {
            AppService.OpenUrl(AppService.PatreonWebPage);
        }

        [RelayCommand]
        internal void CloseDialog()
        {
            dialogService.Close(this);
        }

        [RelayCommand]
        internal static void PayPalDonate()
        {
            string text = DynRes.TryGetString("PayPalDonation");

            string encodedText = HttpUtility.UrlEncode(text);

            string url = $"https://www.paypal.com/donate/?business=DBUQU9W2LNS2G&no_recurring=0&item_name={encodedText}&currency_code=EUR";

            AppService.OpenUrl(url);
        }
    }
}