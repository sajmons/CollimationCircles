using Avalonia;
using CollimationCircles.Services;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CollimationCircles
{
    internal class Program
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private static bool IsLinuxArm64 =>
            OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

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
        /// On Linux ARM64 (e.g. Raspberry Pi Bookworm), Avalonia's default OpenGL
        /// rendering can crash due to GPU driver incompatibilities.
        /// Force software rendering via LIBGL to prevent native crashes.
        /// </summary>
        private static void ConfigureLinuxEnvironment()
        {
            if (IsLinuxArm64)
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LIBGL_ALWAYS_SOFTWARE")))
                {
                    Environment.SetEnvironmentVariable("LIBGL_ALWAYS_SOFTWARE", "1");
                    logger.Info("Set LIBGL_ALWAYS_SOFTWARE=1 for ARM64 Linux");
                }

                logger.Info("Applied Linux ARM64 rendering workarounds");
            }
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
        {
            var builder = AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();

            // On Linux ARM64 (Raspberry Pi), force software rendering to avoid
            // GPU driver segfaults with OpenGL on Bookworm.
            if (IsLinuxArm64)
            {
                builder = builder.With(new X11PlatformOptions
                {
                    RenderingMode = [X11RenderingMode.Software]
                });

                logger.Info("Configured Avalonia X11 software rendering for ARM64 Linux");
            }

            return builder;
        }
    }
}
