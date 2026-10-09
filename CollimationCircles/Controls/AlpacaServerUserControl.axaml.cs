using Avalonia.Controls;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace CollimationCircles.Controls
{
    public partial class AlpacaServerUserControl : UserControl
    {
        public AlpacaServerUserControl()
        {
            InitializeComponent();

            if (!Design.IsDesignMode)
            {
                DataContext = Ioc.Default.GetRequiredService<StreamViewModel>();
            }
        }
    }
}
