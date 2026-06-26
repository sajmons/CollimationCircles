using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CollimationCircles.Services;
using CollimationCircles.ViewModels;
using CollimationCircles.Views;
using CommunityToolkit.Mvvm.DependencyInjection;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.Avalonia;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
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
        AppService.LogSystemInformation();

        ConfigureServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            SettingsViewModel vm = Ioc.Default.GetRequiredService<SettingsViewModel>();

            vm.LoadState();

            desktop.MainWindow = new MainView
            {
                Topmost = vm.AlwaysOnTop
            };

            // LibVLC initialization is now lazy (see LibVLCService.EnsureInitialized).
            // We no longer probe IsAvailable at startup because the native libvlc
            // constructor can segfault on some platforms (e.g. Linux ARM64 with VLC
            // 3.0.x from Debian trixie) and take the whole process down.  The
            // compatibility message is shown on first Play attempt instead.
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices()
    {
        IResourceService resourceService = new ResourceService(AppService.LangDir);

        Ioc.Default.ConfigureServices(
        new ServiceCollection()
            .AddSingleton<IResourceService>(resourceService)
            .AddSingleton<ILicenseService>(new LicenseService(AppService.ProductName, resourceService))
            .AddSingleton<IDialogService>(new DialogService(
                new DialogManager(
                    viewLocator: new ViewLocator(),
                    dialogFactory: new DialogFactory()
                        .AddMessageBox()),
                viewModelFactory: x => Ioc.Default.GetService(x)))
            .AddSingleton<SettingsViewModel>()
            .AddSingleton<StreamViewModel>()
            .AddSingleton<AppLogViewModel>()
            .AddSingleton<CameraControlsViewModel>()
            .AddSingleton<ProfileManagerViewModel>()
            .AddTransient<AboutViewModel>()
            .AddSingleton<RequestLicenseViewModel>()
            .AddSingleton<CollimationAnalysisViewModel>()
            .AddTransient<IDrawHelperService, DrawHelperService>()
            .AddSingleton<IKeyHandlingService, KeyHandlingService>()
            .AddSingleton<ICameraControlService, CameraControlService>()
            .AddSingleton<ILibVLCService, LibVLCService>()
            .AddTransient<ImageViewModel>()
            .BuildServiceProvider());
    }
}