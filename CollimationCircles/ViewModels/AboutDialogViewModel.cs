using CollimationCircles.Resources.Strings;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;

namespace CollimationCircles.ViewModels
{
    public partial class AboutDialogViewModel : BaseViewModel, IModalDialogViewModel
    {
        [ObservableProperty]
        public string appDescription;        

        public bool? DialogResult => true;

        private readonly IAppService appService;

        public AboutDialogViewModel(IAppService appService)
        {
            this.appService = appService;
            Title = $"{Text.About} {Text.CollimationCircles} {appService.GetAppVersion()}";
            AppDescription = $"{Text.AppDescription}\n{Text.Author} {Text.Copyright}";
        }

        [RelayCommand]
        internal void OpenWebSite()
        {
            OpenUrl(appService.WebPage);
        }

        [RelayCommand]
        internal void OpenContactWebPage()
        {
            OpenUrl(appService.ContactPage);
        }
    }
}
