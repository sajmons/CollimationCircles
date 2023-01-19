using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CollimationCircles.ViewModels;
using CollimationCircles.Views;
using CommunityToolkit.Mvvm.DependencyInjection;
using HanumanInstitute.MvvmDialogs.Avalonia;
using HanumanInstitute.MvvmDialogs;
using Microsoft.Extensions.DependencyInjection;
using CollimationCircles.Services;

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

            if (vm != null)
            {
                desktop.MainWindow.Position = vm.Position;
            }

            MoveWindowService? mws = Ioc.Default.GetService<MoveWindowService>();

            desktop.MainWindow.KeyDown += (s, e) =>
            {
                mws?.HandleMovement(desktop.MainWindow, vm, e);
                mws?.HandleScale(vm, e);
                mws?.HandleRotation(vm, e);
            };

            desktop.MainWindow.Closing += (s, e) =>
            {
                vm?.SaveState();
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
            .AddSingleton<MoveWindowService>()
            .BuildServiceProvider());
    }        
}