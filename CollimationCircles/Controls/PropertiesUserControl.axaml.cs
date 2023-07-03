using Avalonia.Controls;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace CollimationCircles.Controls
{
    public partial class PropertiesUserControl : UserControl
    {
        public PropertiesUserControl()
        {
            InitializeComponent();
            DataContext = Ioc.Default.GetService<SettingsViewModel>();
        }
    }
}
