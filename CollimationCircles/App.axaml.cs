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

            desktop.MainWindow = new MainView
            {
                Topmost = vm?.AlwaysOnTop ?? false
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
            .AddSingleton<StreamViewModel>()
            .AddSingleton<AppLogViewModel>()
            .AddTransient<AboutViewModel>()            
            .AddTransient<IDrawHelperService, DrawHelperService>()
            .AddSingleton<IKeyHandlingService, KeyHandlingService>()
            .BuildServiceProvider());
    }
}