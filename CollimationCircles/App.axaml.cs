using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using CollimationCircles.Services;
using CollimationCircles.ViewModels;
using CollimationCircles.Views;
using CommunityToolkit.Mvvm.DependencyInjection;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.Avalonia;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;

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
            ILibVLCService libVLCService = Ioc.Default.GetRequiredService<ILibVLCService>();

            vm.LoadState();

            desktop.MainWindow = new MainView
            {
                Topmost = vm.AlwaysOnTop
            };

            if (!libVLCService.IsAvailable)
            {
                if (desktop.MainWindow.IsVisible)
                {
                    _ = ShowLibVlcCompatibilityMessageAsync(desktop.MainWindow);
                }
                else
                {
                    async void OnOpened(object? sender, System.EventArgs args)
                    {
                        desktop.MainWindow.Opened -= OnOpened;
                        await ShowLibVlcCompatibilityMessageAsync(desktop.MainWindow);
                    }

                    desktop.MainWindow.Opened += OnOpened;
                }
            }
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

    private static async Task ShowLibVlcCompatibilityMessageAsync(Window owner)
    {
        IResourceService resourceService = Ioc.Default.GetRequiredService<IResourceService>();

        string message =
            resourceService.TryGetString("LibVlcCompatibilityBody1") + "\n\n" +
            resourceService.TryGetString("LibVlcCompatibilityBody2") + "\n" +
            resourceService.TryGetString("LibVlcCompatibilityBody3") + "\n" +
            resourceService.TryGetString("LibVlcCompatibilityBody4") + "\n" +
            resourceService.TryGetString("LibVlcCompatibilityBody5") + "\n" +
            resourceService.TryGetString("LibVlcCompatibilityBody6");

        var dialog = new Window
        {
            Title = resourceService.TryGetString("LibVlcCompatibilityTitle"),
            Width = 620,
            MinWidth = 520,
            Height = 360,
            MinHeight = 300,
            CanResize = true,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Topmost = true,
            ExtendClientAreaToDecorationsHint = false,
            TransparencyLevelHint = [WindowTransparencyLevel.None],
            Background = new SolidColorBrush(Color.Parse("#202124")),
            Opacity = 1.0
        };

        var okButton = new Button
        {
            Content = "OK",
            MinWidth = 120,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        okButton.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    Foreground = new SolidColorBrush(Color.Parse("#F1F3F4")),
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Stretch
                },
                okButton
            }
        };

        await dialog.ShowDialog(owner);
    }
}