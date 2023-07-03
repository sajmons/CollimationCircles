using Avalonia.Controls;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace CollimationCircles.Controls
{
    public partial class ItemPropertiesUserControl : UserControl
    {
        public ItemPropertiesUserControl()
        {
            InitializeComponent();
            DataContext = Ioc.Default.GetService<SettingsViewModel>();
        }
    }
}
