using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CollimationCircles.ViewModels;
using CollimationCircles.Views;
using CommunityToolkit.Mvvm.DependencyInjection;
using HanumanInstitute.MvvmDialogs.Avalonia;
using HanumanInstitute.MvvmDialogs;
using Microsoft.Extensions.DependencyInjection;

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
                desktop.MainWindow = new MainWindow();

                var vm = Ioc.Default.GetService<MainViewModel>();

                if (vm != null)
                {
                    desktop.MainWindow.Position = vm.Position;
                }

                desktop.MainWindow.KeyDown += (s, e) =>
                {
                    int x = desktop.MainWindow.Position.X;
                    int y = desktop.MainWindow.Position.Y;
                    int increment = 1;

                    if (e.KeyModifiers == Avalonia.Input.KeyModifiers.Shift)
                    {
                        increment = 10;
                    }
                    else
                    {
                        increment = 1;
                    }

                    if (e.Key == Avalonia.Input.Key.Up)
                    {
                        y -= increment;
                    }

                    if (e.Key == Avalonia.Input.Key.Down)
                    {
                        y += increment;
                    }

                    if (e.Key == Avalonia.Input.Key.Left)
                    {
                        x -= increment;
                    }

                    if (e.Key == Avalonia.Input.Key.Right)
                    {
                        x += increment;
                    }

                    PixelPoint newP = new(x, y);

                    desktop.MainWindow.Position = newP;

                    if (vm != null)
                    {
                        vm.Position = newP;
                    }
                };
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void ConfigureServices()
        {
            Ioc.Default.ConfigureServices(
            new ServiceCollection()
                .AddSingleton<IDialogService>(new DialogService(
                    new DialogManager(viewLocator: new ViewLocator()),
                    viewModelFactory: x => Ioc.Default.GetService(x)))
                .AddSingleton<MainViewModel>()
                .BuildServiceProvider());
        }
    }
}