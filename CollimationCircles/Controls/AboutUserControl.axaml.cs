using Avalonia.Controls;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace CollimationCircles.Controls
{
    public partial class AboutTabUserControl : UserControl
    {
        public AboutTabUserControl()
        {
            InitializeComponent();

            var vm = Ioc.Default.GetRequiredService<AboutViewModel>();

            DataContext = vm;
        }
    }
}
