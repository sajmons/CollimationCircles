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

            SettingsViewModel? vm = Ioc.Default.GetService<SettingsViewModel>();

            DataContext = vm;

            Position = vm?.SettingsWindowPosition ?? new Avalonia.PixelPoint();

            WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) =>
            {
                Topmost = m.Value.AlwaysOnTop;                
            });
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);

            var vm = Ioc.Default.GetService<SettingsViewModel>();

            if (vm is not null)
            {
                vm.SettingsWindowPosition = Position;
                vm.SettingsWindowWidth = Width;
                vm.SettingsWindowHeight = Height;

                if (e.CloseReason == WindowCloseReason.WindowClosing)
                {
                    vm.DockInMainWindow = true;
                }
            }
        }
    }
}