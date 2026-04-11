using Avalonia.Controls;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace CollimationCircles.Views
{
    public partial class RequestLicenseView : Window
    {
        public RequestLicenseView()
        {
            InitializeComponent();

            if (!Design.IsDesignMode)
            {
                DataContext = Ioc.Default.GetRequiredService<RequestLicenseViewModel>();
            }
        }
    }
}
