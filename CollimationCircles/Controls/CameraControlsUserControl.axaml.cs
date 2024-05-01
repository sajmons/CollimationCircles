using Avalonia.Controls;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace CollimationCircles.Controls
{
    public partial class CameraControlsUserControl : UserControl
    {
        public CameraControlsUserControl()
        {
            InitializeComponent();

            if (!Design.IsDesignMode)
            {
                DataContext = Ioc.Default.GetRequiredService<CameraControlsViewModel>();
            }
        }    
    }
}
