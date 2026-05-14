using Avalonia.Controls;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace CollimationCircles.Controls
{
    public partial class DynamicCameraControlsUserControl : UserControl
    {
        public DynamicCameraControlsUserControl()
        {
            InitializeComponent();            

            DataContext = Ioc.Default.GetRequiredService<CameraControlsViewModel>();
        }
    }
}
