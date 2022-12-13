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