using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CollimationCircles.Services;
using CollimationCircles.ViewModels;
using CollimationCircles.Views;
using CommunityToolkit.Mvvm.DependencyInjection;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.Avalonia;
using Microsoft.Extensions.DependencyInjection;

namespace CollimationCircles;
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
            SettingsViewModel? vm = Ioc.Default.GetService<SettingsViewModel>();

            vm?.LoadState();

            desktop.MainWindow = new MainView();

            MoveWindowService? mws = Ioc.Default.GetService<MoveWindowService>();

            desktop.MainWindow.KeyDown += (s, e) =>
            {
                mws?.HandleMovement(desktop.MainWindow, vm, e);
                mws?.HandleGlobalScale(vm, e);
                mws?.HandleHelperRadius(vm, e);
                mws?.HandleGlobalRotation(vm, e);
                mws?.HandleHelperRotation(vm, e);
                mws?.HandleHelperCount(vm, e);
                mws?.HandleHelperThickness(vm, e);
                mws?.HandleHelperSpacing(vm, e);
            };

            desktop.MainWindow.Opened += (s, e) =>
            {
                desktop.MainWindow.Position = vm != null ? vm.Position : new PixelPoint();
            };

            desktop.MainWindow.Closing += (s, e) =>
            {
                vm?.SaveState();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices()
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
            .AddSingleton<MoveWindowService>()
            .BuildServiceProvider());
    }
}