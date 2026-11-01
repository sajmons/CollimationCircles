using Avalonia.Controls;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace CollimationCircles.Controls
{
    public partial class LiveAnalysisUserControl : UserControl
    {
        public LiveAnalysisUserControl()
        {
            InitializeComponent();
            if (!Design.IsDesignMode)
            {
                DataContext = Ioc.Default.GetRequiredService<CollimationAnalysisViewModel>();
            }
        }
    }
}
