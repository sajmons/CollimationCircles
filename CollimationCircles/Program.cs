using Avalonia;
using Avalonia.Media.Imaging;
using CollimationCircles.Services;
using System;
using System.Runtime.InteropServices;

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
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                logger.Fatal($"Unhandled exception: {e.ExceptionObject}");
                NLog.LogManager.Shutdown();
            };

            try
            {
                ConfigureLinuxEnvironment();

                AppService.LogSystemInformation();

                BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args, Avalonia.Controls.ShutdownMode.OnMainWindowClose);
            }
            catch (Exception ex)
            {
                logger.Fatal(ex.Message);
                throw;
            }
            finally
            {
                NLog.LogManager.Shutdown();
            }
        }

        /// <summary>
        /// On Linux ARM64 (e.g. Raspberry Pi Bookworm), Avalonia's default Wayland/OpenGL
        /// rendering can crash due to GPU driver incompatibilities.
        /// Force X11 backend and software rendering to prevent native crashes.
        /// </summary>
        private static void ConfigureLinuxEnvironment()
        {
            if (OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                // Force X11 instead of Wayland to avoid compositor crashes
                Environment.SetEnvironmentVariable("AVALONIA_SCREEN_SCALE_FACTORS", null);

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LIBGL_ALWAYS_SOFTWARE")))
                {
                    Environment.SetEnvironmentVariable("LIBGL_ALWAYS_SOFTWARE", "1");
                    logger.Info("Set LIBGL_ALWAYS_SOFTWARE=1 for ARM64 Linux");
                }

                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("AVALONIA_RENDER_MODE")))
                {
                    // Prefer software rendering to avoid GPU driver crashes on Pi
                    Environment.SetEnvironmentVariable("AVALONIA_RENDER_MODE", "software");
                    logger.Info("Set AVALONIA_RENDER_MODE=software for ARM64 Linux");
                }

                logger.Info("Applied Linux ARM64 rendering workarounds");
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
