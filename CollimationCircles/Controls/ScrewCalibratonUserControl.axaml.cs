using Avalonia.Controls;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace CollimationCircles.Controls
{
    public partial class ScrewCalibrationUserControl : UserControl
    {
        public ScrewCalibrationUserControl()
        {
            InitializeComponent();
            if (!Design.IsDesignMode)
            {
                DataContext = Ioc.Default.GetRequiredService<ScrewCalibrationViewModel>();
            }
        }
    }
}
