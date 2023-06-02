using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.Avalonia;
using MessageBox.Avalonia.Extensions;
using System.ComponentModel;
using System.Threading.Tasks;

namespace CollimationCircles.ViewModels
{
    public partial class AboutDialogViewModel : BaseViewModel, IModalDialogViewModel
    {
        [ObservableProperty]
        public string author = "Collimation Circles\nAuthor: Simon Šander";

        [ObservableProperty]
        public string webSite = "https://saimons-astronomy.webador.com/software/collimation-circles";

        public bool? DialogResult => true;

        public AboutDialogViewModel()
        {
            Title = "About";
        }

        [RelayCommand]
        internal void OpenWebSite()
        {
            OpenUrl(WebSite);
        }

        [RelayCommand]
        internal void Close()
        {
            
        }
    }
}
