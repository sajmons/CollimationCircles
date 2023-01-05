using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CollimationCircles.ViewModels;
using CollimationCircles.Views;
using CommunityToolkit.Mvvm.DependencyInjection;
using HanumanInstitute.MvvmDialogs.Avalonia;
using HanumanInstitute.MvvmDialogs;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Input;
using Avalonia.Controls;
using CollimationCircles.Services;

namespace CollimationCircles
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            ConfigureServices();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainView();

                SettingsViewModel? vm = Ioc.Default.GetService<SettingsViewModel>();

                if (vm != null)
                {
                    desktop.MainWindow.Position = vm.Position;
                }

                desktop.MainWindow.KeyDown += (s, e) =>
                {
                    HandleMovement(desktop.MainWindow, vm, e);
                    HandleScale(vm, e);
                    HandleRotation(vm, e);
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void ConfigureServices()
        {
            Ioc.Default.ConfigureServices(
            new ServiceCollection()
                .AddSingleton<IDialogService>(new DialogService(
                    new DialogManager(
                        viewLocator: new ViewLocator(),
                        dialogFactory: new DialogFactory()
                            .AddMessageBox()),
                    viewModelFactory: x => Ioc.Default.GetService(x)))
                .AddSingleton<SettingsViewModel>()
                .AddTransient<IDrawHelperService, DrawHelperService>()
                .AddTransient<IAppService, AppService>()
                .BuildServiceProvider());
        }

        private void HandleMovement(Window window, SettingsViewModel? vm, KeyEventArgs e)
        {
            if (!e.Handled)
            {
                int x = window.Position.X;
                int y = window.Position.Y;
                int increment = 1;                

                if (vm != null)
                {
                    switch (e.Key)
                    {
                        case Key.Up:
                            y -= increment;
                            e.Handled = true;
                            break;

                        case Key.Down:
                            y += increment;
                            e.Handled = true;
                            break;

                        case Key.Left:
                            x -= increment;
                            e.Handled = true;
                            break;
                        case Key.Right:
                            x += increment;
                            e.Handled = true;
                            break;
                    }

                    window.Position = new PixelPoint(x, y);

                    vm.Position = window.Position;
                }
            }
        }

        private void HandleScale(SettingsViewModel? vm, KeyEventArgs e)
        {
            if (!e.Handled)
            {
                if (vm != null)
                {
                    double increment = vm.Scale;

                    switch (e.Key)
                    {
                        case Key.Add:
                        case Key.OemPlus:
                            increment += 0.01;
                            e.Handled = true;
                            break;

                        case Key.Subtract:
                        case Key.OemMinus:
                            increment -= 0.01;
                            e.Handled = true;
                            break;
                    }

                    if (e.Handled)
                    {
                        vm.Scale = increment;
                    }
                }
            }
        }

        private void HandleRotation(SettingsViewModel? vm, KeyEventArgs e)
        {
            if (!e.Handled)
            {
                if (vm != null)
                {
                    double rotation = vm.RotationAngle;

                    switch (e.Key)
                    {
                        case Key.R:
                            rotation += 1;
                            e.Handled = true;
                            break;
                        case Key.L:
                            rotation -= 1;
                            e.Handled = true;
                            break;
                    }

                    if (e.Handled)
                    {
                        vm.RotationAngle = rotation;
                    }
                }
            }
        }
    }
}