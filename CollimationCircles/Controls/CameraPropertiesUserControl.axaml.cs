using Avalonia.Controls;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace CollimationCircles.Controls
{
    public partial class CameraPropertiesUserControl : UserControl
    {
        public CameraPropertiesUserControl()
        {
            InitializeComponent();

            if (!Design.IsDesignMode)
            {
                DataContext = Ioc.Default.GetService<CameraControlsViewModel>();
            }
        }    
    }
}
