using Avalonia.Controls;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace CollimationCircles.Views
{
    public partial class CameraControlsView : Window
    {
        public CameraControlsView()
        {
            InitializeComponent();

            if (!Design.IsDesignMode)
            {
                DataContext = Ioc.Default.GetRequiredService<CameraControlsViewModel>();
            }
        }
    }
}
