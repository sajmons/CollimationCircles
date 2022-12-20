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
using System;
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
                desktop.MainWindow = new MainWindow();

                MainViewModel? vm = Ioc.Default.GetService<MainViewModel>();

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
                    new DialogManager(viewLocator: new ViewLocator()),
                    viewModelFactory: x => Ioc.Default.GetService(x)))
                .AddSingleton<MainViewModel>()
                .AddTransient<IDrawHelperService, DrawHelperService>()
                .BuildServiceProvider());
        }

        private void HandleMovement(Window window, MainViewModel? vm, KeyEventArgs e)
        {
            int x = window.Position.X;
            int y = window.Position.Y;
            int increment;

            if (e.KeyModifiers == KeyModifiers.Control) return;
            if (e.KeyModifiers == KeyModifiers.Alt) return;

            if (e.KeyModifiers == KeyModifiers.Shift)
            {
                increment = 10;
            }
            else
            {
                increment = 1;
            }

            if (e.Key == Key.Up)
            {
                y -= increment;
            }

            if (e.Key == Key.Down)
            {
                y += increment;
            }

            if (e.Key == Key.Left)
            {
                x -= increment;
            }

            if (e.Key == Key.Right)
            {
                x += increment;
            }

            PixelPoint newP = new(x, y);

            window.Position = newP;

            if (vm != null)
            {
                vm.Position = newP;
            }
        }

        private void HandleScale(MainViewModel? vm, KeyEventArgs e)
        {
            if (e.KeyModifiers == KeyModifiers.Shift) return;
            if (e.KeyModifiers == KeyModifiers.Alt) return;

            if (e.KeyModifiers == KeyModifiers.Control && vm != null)
            {
                double increment = vm.Scale;

                if (e.Key == Key.Up)
                {
                    increment += 0.01;
                }

                if (e.Key == Key.Down)
                {
                    increment -= 0.01;
                }

                vm.Scale = increment;
            }
        }

        private void HandleRotation(MainViewModel? vm, KeyEventArgs e)
        {
            if (e.KeyModifiers == KeyModifiers.Shift) return;
            if (e.KeyModifiers == KeyModifiers.Control) return;

            if (e.KeyModifiers == KeyModifiers.Alt && vm != null)
            {
                double rotation = vm.RotationAngle;

                if (e.Key == Key.Up)
                {
                    rotation += 1;
                }

                if (e.Key == Key.Down)
                {
                    rotation -= 1;
                }

                vm.RotationAngle = rotation;
            }
        }
    }
}