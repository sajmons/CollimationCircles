using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using CollimationCircles.Messages;
using CollimationCircles.Services;
using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;

namespace CollimationCircles.Views
{
    public partial class SettingsView : Window
    {
        private readonly IKeyHandlingService? khs;
        private readonly SettingsViewModel? vm;

        public SettingsView()
        {
            InitializeComponent();

            vm = Ioc.Default.GetService<SettingsViewModel>();

            DataContext = vm;

            Position = vm?.SettingsWindowPosition ?? new Avalonia.PixelPoint();

            WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) =>
            {
                Topmost = m.Value.AlwaysOnTop;
            });

            khs = Ioc.Default.GetService<IKeyHandlingService>();
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

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow != null)
                {
                    khs?.HandleMovement(desktop.MainWindow, vm, e);
                }
            }

            khs?.HandleGlobalScale(vm, e);
            khs?.HandleHelperRadius(vm, e);
            khs?.HandleGlobalRotation(vm, e);
            khs?.HandleHelperRotation(vm, e);
            khs?.HandleHelperCount(vm, e);
            khs?.HandleHelperThickness(vm, e);
            khs?.HandleHelperSpacing(vm, e);

            base.OnKeyDown(e);
        }
    }
}