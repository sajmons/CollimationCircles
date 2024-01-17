using CollimationCircles.Messages;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using NLog;
using NLog.Targets;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CollimationCircles.ViewModels
{
    public partial class AppLogViewModel : BaseViewModel
    {
        private readonly MemoryTarget memoryTarget;
#pragma warning disable IDE0052 // Remove unread private members
        private readonly Timer timer;
#pragma warning restore IDE0052 // Remove unread private members

        [ObservableProperty]
        private string logContent = "LOG";

        [ObservableProperty]
        private bool showApplicationLog = false;

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public AppLogViewModel(SettingsViewModel settingsViewModel)
        {
            ShowApplicationLog = settingsViewModel.ShowApplicationLog;

            memoryTarget = (MemoryTarget)LogManager.Configuration.FindTargetByName("memory");

            timer = new Timer(
                new TimerCallback(TickTimer),
                null,
                1000,
                1000);

            WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) =>
            {
                ShowApplicationLog = m.Value.ShowApplicationLog;
            });
        }

        private void TickTimer(object? state)
        {
            var log = string.Join("\r\n", memoryTarget.Logs);
            LogContent = log;
        }

        [RelayCommand]
        internal static async Task ShowLogFileLocation()
        {
            string logPath = AppDomain.CurrentDomain.BaseDirectory + "logs";

            logger.Info($"Open log file path in file browser '{logPath}'");

            await AppService.OpenFileBrowser(logPath);
        }
    }
}
