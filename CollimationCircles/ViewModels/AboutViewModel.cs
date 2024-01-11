using CollimationCircles.Helper;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using System;

namespace CollimationCircles.ViewModels
{
    internal partial class AboutViewModel : BaseViewModel, IModalDialogViewModel
    {
        public bool? DialogResult => true;
        
        private readonly IAppService appService;
        private readonly IDialogService dialogService;


        public AboutViewModel(IAppService appService, IDialogService dialogService)
        {
            this.appService = appService;
            this.dialogService = dialogService;

            Title = $"{DynRes.TryGetString("About")} - {DynRes.TryGetString("CollimationCircles")} - {DynRes.TryGetString("Version")} {appService?.GetAppVersion()}";
        }

        [RelayCommand]
        internal void OpenPatreonWebSite()
        {
            OpenUrl(appService.PatreonWebPage);
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