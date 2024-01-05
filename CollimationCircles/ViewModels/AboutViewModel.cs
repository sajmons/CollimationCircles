using CollimationCircles.Helper;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using System;

namespace CollimationCircles.ViewModels
{
    internal partial class AboutViewModel : BaseViewModel, IModalDialogViewModel, ICloseable
    {
        public bool? DialogResult => true;
        
        public event EventHandler? RequestClose;

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
    }
}