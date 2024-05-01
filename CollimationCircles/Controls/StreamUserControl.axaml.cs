using Avalonia.Controls;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace CollimationCircles.Controls
{
    public partial class StreamUserControl : UserControl
    {
        public StreamUserControl()
        {
            InitializeComponent();

            if (!Design.IsDesignMode)
            {
                DataContext = Ioc.Default.GetRequiredService<StreamViewModel>();
            }
        }
    }
}
