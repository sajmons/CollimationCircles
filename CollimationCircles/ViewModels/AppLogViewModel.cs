using CommunityToolkit.Mvvm.ComponentModel;
using NLog;
using NLog.Targets;
using System.Threading;

namespace CollimationCircles.ViewModels
{
    internal partial class AppLogViewModel : BaseViewModel
    {
        private readonly MemoryTarget memoryTarget;
#pragma warning disable IDE0052 // Remove unread private members
        private readonly Timer timer;
#pragma warning restore IDE0052 // Remove unread private members

        [ObservableProperty]
        private string logContent = "LOG";

        [ObservableProperty]
        private bool showApplicationLog = false;
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
    }
}
