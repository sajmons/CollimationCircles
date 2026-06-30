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
        internal void CloseDialog()
        {
            DialogService.Close(this);
        }

        [RelayCommand]
        internal void Donate()
        {
            string text = ResSvc.TryGetString("Donation");

            string url = $"https://saimons.gumroad.com/l/kilfjp";

            AppService.OpenUrl(url);
        }

        [RelayCommand]
        internal static void OpenContactWebPage()
        {
            AppService.OpenUrl(AppService.ContactPage);
        }

        [RelayCommand]
        internal static void OpenGitHubPage()
        {
            AppService.OpenUrl(AppService.GitHubPage);
        }

        [RelayCommand]
        internal static void OpenTwitter()
        {
            AppService.OpenUrl(AppService.TwitterPage);
        }

        [RelayCommand]
        internal static void OpenYouTubeChannel()
        {
            AppService.OpenUrl(AppService.YouTubeChannel);
        }

        [RelayCommand]
        internal static void OpenPatreonWebSite()
        {
            AppService.OpenUrl(AppService.PatreonWebPage);
        }

        [RelayCommand]
        internal static void GitHubIssue()
        {
            AppService.OpenUrl(AppService.GitHubIssue);
        }
    }
}