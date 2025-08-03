using Avalonia.Controls;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace CollimationCircles.Controls
{
    public partial class AIUserControl : UserControl
    {
        public AIUserControl()
        {
            InitializeComponent();

            if (!Design.IsDesignMode)
            {
                DataContext = Ioc.Default.GetRequiredService<AIViewModel>();
            }
        }
    }
}
