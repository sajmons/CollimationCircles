using Avalonia.Controls;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace CollimationCircles.Controls
{
    public partial class LiveAnalysisResultUserControl : UserControl
    {
        public LiveAnalysisResultUserControl()
        {
            InitializeComponent();
            if (!Design.IsDesignMode)
            {
                DataContext = Ioc.Default.GetRequiredService<CollimationAnalysisViewModel>();
            }
        }
    }
}
