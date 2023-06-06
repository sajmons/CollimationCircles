using Avalonia.Controls;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace CollimationCircles.Views
{
    public partial class SettingsView : Window
    {
        public SettingsView()
        {            
            InitializeComponent();
            DataContext = Ioc.Default.GetService<SettingsViewModel>();
        }
    }
}