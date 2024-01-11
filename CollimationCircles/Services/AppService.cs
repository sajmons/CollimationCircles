using Newtonsoft.Json;
using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CollimationCircles.Services;
public class AppService
{
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
    private const string stateFile = "appstate.json";    
    private static readonly string owner = "sajmons";
    private static readonly string reponame = "CollimationCircles";

    public const string LIBCAMERA_VID = "libcamera-vid";
    public const string LIBCAMERA_APPS = "libcamera-apps";
    public const string VLC = "vlc";
    public const string LIBVLC_DEV = "libvlc-dev";

    public const string WebPage = "https://saimons-astronomy.webador.com/software/collimation-circles";
    public const string ContactPage = "https://saimons-astronomy.webador.com/about";
    public const string GitHubPage = "https://github.com/sajmons/CollimationCircles";
    public const string TwitterPage = "https://twitter.com/saimons_art";
    public const string YouTubeChannel = "https://www.youtube.com/channel/UCz6iFL9ziUcWgs_n6n2gwZw";
    public const string GitHubIssue = "https://github.com/sajmons/CollimationCircles/issues/new";
    public const string PatreonWebPage = "https://www.patreon.com/SaimonsAstronomy";

    public static string GetAppVersion()
    {
        var entryAssembly = Assembly.GetEntryAssembly();

        var assemblyVersion = entryAssembly?.GetName().Version;

        return assemblyVersion?.ToString() ?? "0.0.0";
    }

    public static bool SameVersion(string v1, string v2)
    {
        return new Version(v1) == new Version(v2);
    }

    public static T? Deserialize<T>(string jsonState)
    {
        return JsonConvert.DeserializeObject<T>(jsonState,
            new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                NullValueHandling = NullValueHandling.Ignore,
            });
    }

    public static string Serialize<T>(T obj)
    {
        return JsonConvert.SerializeObject(obj, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto
        });
    }

    public static T? LoadState<T>(string? fileName = null)
    {
        logger.Info($"Loading application state from '{fileName ?? stateFile}'");

        var jsonState = File.ReadAllText(fileName ?? stateFile);

        return Deserialize<T>(jsonState);
    }

    public static void SaveState<T>(T obj, string? fileName = null)
    {
        var jsonState = Serialize<T>(obj);

        File.WriteAllText(fileName ?? stateFile, jsonState, System.Text.Encoding.UTF8);

        logger.Info($"Saving application state to '{fileName ?? stateFile}'");
    }

    public static async Task<(bool, string, string)> DownloadUrl(string currentVersion)
    {
        try
        {
            GitHubClient client = new(new ProductHeaderValue("CollimationCircles"));

            var release = await client.Repository.Release.GetLatest(owner, reponame);

            var gitHubVer = release.TagName.Split('-')[1];

            var osa = GetOSAndArch();

            if (osa is not null)
            {
                Version oldVersion = new(currentVersion);

                Version newVersion = new(gitHubVer);

                var asset = release.Assets.FirstOrDefault(x => x.Name.Contains(osa));

                if (asset is not null)
                {
                    if (newVersion > oldVersion)
                        return (true, asset.BrowserDownloadUrl, newVersion.ToString());
                    else
                        return (true, string.Empty, string.Empty);
                }
                else
                    return (true, string.Empty, string.Empty);
            }
            else
                return (true, string.Empty, string.Empty);
        }
        catch (Exception exc)
        {
            return (false, exc.Message, string.Empty);
        }
    }

    public static string? GetOSAndArch()
    {
        string? os = null;

        if (OperatingSystem.IsLinux())
            os = "linux";
        else
        if (OperatingSystem.IsWindows())
            os = "win";
        else
        if (OperatingSystem.IsMacOS())
            os = "osx";

        return $"{os}-{RuntimeInformation.ProcessArchitecture}".ToLower() ?? null;
    }

    public static async Task OpenFileBrowser(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            using Process fileOpener = new();
            fileOpener.StartInfo.FileName = "explorer";
            fileOpener.StartInfo.Arguments = "/select," + path + "\"";
            fileOpener.Start();
            await fileOpener.WaitForExitAsync();
            return;
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            using Process fileOpener = new();
            fileOpener.StartInfo.FileName = "explorer";
            fileOpener.StartInfo.Arguments = "-R " + path;
            fileOpener.Start();
            await fileOpener.WaitForExitAsync();
            return;
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            using Process dbusShowItemsProcess = new()
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dbus-send",
                    Arguments = "--print-reply --dest=org.freedesktop.FileManager1 /org/freedesktop/FileManager1 org.freedesktop.FileManager1.ShowItems array:string:\"file://" + path + "\" string:\"\"",
                    UseShellExecute = true
                }
            };
            dbusShowItemsProcess.Start();
            await dbusShowItemsProcess.WaitForExitAsync();

            if (dbusShowItemsProcess.ExitCode == 0)
            {
                // The dbus invocation can fail for a variety of reasons:
                // - dbus is not available
                // - no programs implement the service,
                // - ...
                return;
            }
        }

        using Process folderOpener = new();
        folderOpener.StartInfo.FileName = System.IO.Path.GetDirectoryName(path);
        folderOpener.StartInfo.UseShellExecute = true;
        folderOpener.Start();
        await folderOpener.WaitForExitAsync();
    }

    public static Task<bool> IsPackageInstalled(string package)
    {
        var tcs = new TaskCompletionSource<bool>();

        var t1 = Task.Run(async () =>
        {
            // dpkg-query -W -f='${Status} ${Version}\n' vlc
            var (exitCode, output, process) = await ExecuteCommand("dpkg-query", [$"-W", "-f=${Status}; ${Version}\n", $"{package}"]);            
            tcs.TrySetResult(exitCode == 0 && output.StartsWith("install ok installed"));
        });

        t1.Wait();

        return tcs.Task;
    }

    public static Task<(int, string, Process)> ExecuteCommand(string fileName, List<string> arguments, Action? started = null, int timeout = -1)
    {
        var tcs = new TaskCompletionSource<(int, string, Process)>();

        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true            
        };

        using Process process = new()
        {
            StartInfo = startInfo
        };

        var argStr = string.Join(' ', arguments);

        try
        {
            arguments.ForEach(startInfo.ArgumentList.Add);

            logger.Debug($"Executing command '{fileName} {argStr}'");

            StringBuilder output = new();
            StringBuilder error = new();

            using AutoResetEvent outputWaitHandle = new(false);
            using AutoResetEvent errorWaitHandle = new(false);

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data == null)
                {
                    outputWaitHandle.Set();
                }
                else
                {
                    output.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data == null)
                {
                    errorWaitHandle.Set();
                }
                else
                {
                    error.AppendLine(e.Data);
                }
            };

            if (process.Start())
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (process.WaitForExit(timeout) &&
                    outputWaitHandle.WaitOne(timeout) &&
                    errorWaitHandle.WaitOne(timeout))
                {
                    started?.Invoke();

                    // Process completed. Check process.ExitCode and output here.
                    string outputStr = $"{output}";
                    logger.Debug($"Command '{fileName} {argStr}' executed. Return code: {process.ExitCode}, Output: {outputStr}");
                    tcs.TrySetResult((process.ExitCode, outputStr, process));
                }
                else
                {
                    // Timed out.
                    logger.Warn($"Timeout '{fileName} {argStr}'");
                    tcs.TrySetResult((process.ExitCode, string.Empty, process));
                }
            }
            else
            {
                // Process failed to start
                logger.Warn($"Failed to execute command '{fileName} {argStr}'");
                tcs.TrySetResult((process.ExitCode, string.Empty, process));
            }
        }
        catch (Exception exc)
        {
            logger.Error($"Failed to execute command '{fileName} {argStr}' '{exc.Message}'");
            tcs.TrySetResult((-1, string.Empty, process));
        }

        return tcs.Task;
    }

    //public static void StartProcess(string fileName, string arguments)
    //{
    //    try
    //    {
    //        ProcessStartInfo startInfo = new()
    //        {
    //            FileName = fileName,
    //            Arguments = arguments
    //        };

    //        Process proc = new()
    //        {
    //            StartInfo = startInfo
    //        };

    //        logger.Debug($"Starting proces '{fileName} {arguments}'");

    //        if (proc.Start())
    //        {
    //            logger.Debug($"Proces '{fileName} {arguments}' started");
    //        }
    //        else
    //        {
    //            logger.Warn($"Failed to start process '{fileName} {arguments}'");
    //        }
    //    }
    //    catch (Exception exc)
    //    {
    //        logger.Error($"Failed to start process '{fileName} {arguments}' '{exc.Message}'");
    //    }
    //}

    //public static void StartTCPCameraStream(string address)
    //{
    //    StartProcess(
    //        LIBCAMERA_VID,
    //        $"-t 0 --inline --nopreview --listen -o tcp://{address}");

    //    //await ExecuteCommand(LIBCAMERA_VID, [$"-t", "0", "--inline", "", "--nopreview", "", "--listen", "", "-o", $"tcp://{address}"]);

    //    //var t1 = Task.Run(async () =>
    //    //{
    //    //    var (exitCode, output, process) = await ExecuteCommand(LIBCAMERA_VID,
    //    //        [$"-t", "0", "--inline", "--nopreview", "--listen", "-o", $"tcp://{address}"], 0);

    //    //    tcs.TrySetResult(process);
    //    //});

    //    //t1.Wait();
    //}

    public static bool CheckRequirements()
    {
        bool result = true;

        if (OperatingSystem.IsLinux())
        {
            result &= IsPackageInstalled(VLC).GetAwaiter().GetResult();
            result &= IsPackageInstalled(LIBVLC_DEV).GetAwaiter().GetResult();
        }

        return result;
    }

    public static void OpenUrl(string url)
    {
        logger.Trace($"Opening external url '{url}'");

        try
        {
            Process.Start(url);
        }
        catch
        {
            // hack because of this: https://github.com/dotnet/corefx/issues/10361
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsLinux())
            {
                Process.Start("xdg-open", url);
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", url);
            }
            else
            {
                throw;
            }
        }
    }    
}
