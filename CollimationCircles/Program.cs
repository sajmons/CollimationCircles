using Avalonia;
using CollimationCircles.Services;
using System;

namespace CollimationCircles
{
    internal class Program
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                if (AppService.CheckRequirements())
                {
                    BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnMainWindowClose);
                }
                else
                {
                    throw new Exception("Application requirements are not met. Try to set 'minLevel' to 'Trace' for 'logconsole' in NLog.config, for more information.");
                }
            }
            catch (Exception ex)
            {
                // here we can work with the exception, for example add it to our log file
                //Log.Fatal(e, "Something very bad happened");
                //var ds = Ioc.Default.GetService<IDialogService>();
                //var vm = Ioc.Default.GetService<MainViewModel>();
                //ds?.ShowMessageBoxAsync(vm, e.Message, "Error");                
                logger.Fatal(ex.Message);
                throw;
            }
            finally
            {
                // This block is optional. 
                // Use the finally-block if you need to clean things up or similar
                //Log.CloseAndFlush();
                NLog.LogManager.Shutdown();
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
        }
    }
}
