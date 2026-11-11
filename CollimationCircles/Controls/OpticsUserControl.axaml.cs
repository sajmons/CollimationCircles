using Avalonia.Controls;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace CollimationCircles.Controls
{
    public partial class OpticsUserControl : UserControl
    {
        public OpticsUserControl()
        {
            InitializeComponent();

            var vm = Ioc.Default.GetRequiredService<SettingsViewModel>();

            DataContext = vm;
        }
    }
}
