using Avalonia;
using CollimationCircles.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace CollimationCircles
{
    internal class Program
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private const string MacArm64BootstrapFlag = "COLLIMATIONCIRCLES_VLC_ENV_BOOTSTRAPPED";

        private static bool IsLinuxArm64 =>
            OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        private static bool IsMacArm64 =>
            OperatingSystem.IsMacOS() && RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

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
                BootstrapMacArm64VlcEnvironment(args);
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

        private static void BootstrapMacArm64VlcEnvironment(string[] args)
        {
            if (!IsMacArm64)
            {
                return;
            }

            if (!TryGetMacVlcPaths(out string libPath, out string pluginPath, out string dataPath))
            {
                logger.Warn("No macOS VLC installation found for arm64 bootstrap.");
                return;
            }

            bool alreadyBootstrapped = string.Equals(
                Environment.GetEnvironmentVariable(MacArm64BootstrapFlag),
                "1",
                StringComparison.Ordinal);

            if (alreadyBootstrapped)
            {
                logger.Info("macOS arm64 VLC bootstrap already applied for this process.");
                return;
            }

            if (NeedsMacVlcBootstrap(libPath, pluginPath, dataPath))
            {
                RelaunchCurrentProcessWithVlcEnvironment(libPath, pluginPath, dataPath, args);
            }
        }

        private static bool NeedsMacVlcBootstrap(string libPath, string pluginPath, string dataPath)
        {
            string? pluginEnv = Environment.GetEnvironmentVariable("VLC_PLUGIN_PATH");
            string? dataEnv = Environment.GetEnvironmentVariable("VLC_DATA_PATH");

            bool pluginMatches = string.Equals(pluginEnv, pluginPath, StringComparison.Ordinal);
            bool dataMatches = string.Equals(dataEnv, dataPath, StringComparison.Ordinal);
            bool dyldHasPath = PathListContains(Environment.GetEnvironmentVariable("DYLD_LIBRARY_PATH"), libPath) ||
                               PathListContains(Environment.GetEnvironmentVariable("DYLD_FALLBACK_LIBRARY_PATH"), libPath);

            return !pluginMatches || !dataMatches || !dyldHasPath;
        }

        private static void RelaunchCurrentProcessWithVlcEnvironment(string libPath, string pluginPath, string dataPath, string[] args)
        {
            string[] commandLineArgs = Environment.GetCommandLineArgs();
            if (commandLineArgs.Length == 0)
            {
                logger.Warn("Unable to relaunch process for macOS arm64 VLC bootstrap: no command line args.");
                return;
            }

            string executable = commandLineArgs[0];
            var relaunchArguments = new List<string>();

            if (executable.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                string? dotnetHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
                if (string.IsNullOrWhiteSpace(dotnetHost))
                {
                    dotnetHost = Process.GetCurrentProcess().MainModule?.FileName;
                }

                executable = string.IsNullOrWhiteSpace(dotnetHost) ? "dotnet" : dotnetHost;
                relaunchArguments.Add(commandLineArgs[0]);
                relaunchArguments.AddRange(commandLineArgs.Skip(1));
            }
            else
            {
                relaunchArguments.AddRange(commandLineArgs.Skip(1));
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false
            };

            foreach (string arg in relaunchArguments)
            {
                startInfo.ArgumentList.Add(arg);
            }

            startInfo.Environment[MacArm64BootstrapFlag] = "1";
            startInfo.Environment["VLC_PLUGIN_PATH"] = pluginPath;
            startInfo.Environment["VLC_DATA_PATH"] = dataPath;
            startInfo.Environment["DYLD_LIBRARY_PATH"] = MergePathList(startInfo.Environment.TryGetValue("DYLD_LIBRARY_PATH", out string? dyldLibraryPath) ? dyldLibraryPath : null, libPath);
            startInfo.Environment["DYLD_FALLBACK_LIBRARY_PATH"] = MergePathList(startInfo.Environment.TryGetValue("DYLD_FALLBACK_LIBRARY_PATH", out string? dyldFallbackPath) ? dyldFallbackPath : null, libPath);

            logger.Info($"Relaunching process with macOS arm64 VLC env. lib='{libPath}' plugins='{pluginPath}'");

            Process.Start(startInfo);
            Environment.Exit(0);
        }

        private static bool TryGetMacVlcPaths(out string libPath, out string pluginPath, out string dataPath)
        {
            foreach (string appPath in GetMacVlcAppPaths())
            {
                libPath = Path.Combine(appPath, "Contents", "MacOS", "lib");
                pluginPath = Path.Combine(appPath, "Contents", "MacOS", "plugins");
                dataPath = Path.Combine(appPath, "Contents", "MacOS", "share");

                if (Directory.Exists(libPath) && Directory.Exists(pluginPath) && Directory.Exists(dataPath))
                {
                    return true;
                }
            }

            libPath = string.Empty;
            pluginPath = string.Empty;
            dataPath = string.Empty;
            return false;
        }

        private static IEnumerable<string> GetMacVlcAppPaths()
        {
            yield return "/Applications/VLC.app";

            const string caskRoot = "/opt/homebrew/Caskroom/vlc";
            if (!Directory.Exists(caskRoot))
            {
                yield break;
            }

            string[] versionDirs;
            try
            {
                versionDirs = Directory.GetDirectories(caskRoot)
                    .OrderByDescending(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch
            {
                yield break;
            }

            foreach (string versionDir in versionDirs)
            {
                yield return Path.Combine(versionDir, "VLC.app");
            }
        }

        private static bool PathListContains(string? pathList, string path)
        {
            if (string.IsNullOrWhiteSpace(pathList))
            {
                return false;
            }

            char separator = Path.PathSeparator;
            return pathList
                .Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(path, StringComparer.Ordinal);
        }

        private static string MergePathList(string? current, string pathToAdd)
        {
            if (string.IsNullOrWhiteSpace(current))
            {
                return pathToAdd;
            }

            char separator = Path.PathSeparator;
            List<string> parts = current
                .Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (!parts.Contains(pathToAdd, StringComparer.Ordinal))
            {
                parts.Insert(0, pathToAdd);
            }

            return string.Join(separator, parts);
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
