using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Octokit;

namespace CollimationCircles.ViewModels
{
    internal partial class SSHViewModel : BaseViewModel
    {
        [ObservableProperty]
        private string hostname = "192.168.1.174";

        [ObservableProperty]
        private int port = 22;

        [ObservableProperty]
        private string username = "simon";

        [ObservableProperty]
        private string password = "24pi12?";

        private readonly VideoStreamService? videoStreamService;

        public SSHViewModel()
        {
            videoStreamService = Ioc.Default.GetService<VideoStreamService>();
        }

        [RelayCommand]
        public void SSHConnect()
        {
            
        }
    }
}