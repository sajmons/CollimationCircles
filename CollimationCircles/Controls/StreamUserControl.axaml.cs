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

            DataContext = Ioc.Default.GetService<StreamViewModel>();
        }
    }
}
