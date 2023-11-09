using CommunityToolkit.Mvvm.ComponentModel;
using NLog;
using NLog.Targets;
using System.Threading;

namespace CollimationCircles.ViewModels
{
    internal partial class AppLogViewModel : BaseViewModel
    {
        private readonly MemoryTarget memoryTarget;
        private readonly Timer timer;

        [ObservableProperty]
        private string logContent = "LOG";

        public AppLogViewModel()
        {
            memoryTarget = (MemoryTarget)LogManager.Configuration.FindTargetByName("memory");
            timer = new Timer(
                new TimerCallback(TickTimer),
                null,
                1000,
                1000);
        }

        private void TickTimer(object? state)
        {
            var log = string.Join("\r\n", memoryTarget.Logs);
            LogContent = log;
        }
    }
}
