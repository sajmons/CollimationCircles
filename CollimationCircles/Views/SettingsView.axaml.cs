using Avalonia.Controls;
using CollimationCircles.Messages;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;

namespace CollimationCircles.Views
{
    public partial class SettingsView : Window
    {
        public SettingsView()
        {            
            InitializeComponent();
            DataContext = Ioc.Default.GetService<SettingsViewModel>();

            WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) =>
            {
                Topmost = m.Value.AlwaysOnTop;                
            });
        }
    }
}