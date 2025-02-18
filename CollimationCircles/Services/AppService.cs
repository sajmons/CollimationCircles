using CommunityToolkit.Diagnostics;
using DeviceId;
using Newtonsoft.Json;
using Octokit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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

    public const string BasePage = "https://www.saimons-astronomy.com";
    public const string WebPage = $"{BasePage}/software/collimation-circles";
    public const string ContactPage = $"{BasePage}/about";
    public const string GitHubPage = "https://github.com/sajmons/CollimationCircles";
    public const string TwitterPage = "https://twitter.com/saimons_art";
    public const string YouTubeChannel = "https://www.youtube.com/channel/UCz6iFL9ziUcWgs_n6n2gwZw";
    public const string GitHubIssue = "https://github.com/sajmons/CollimationCircles/issues/new";
    public const string PatreonWebPage = "https://www.patreon.com/SaimonsAstronomy";
    public const string LangDir = "CollimationCircles/Resources/Lang";
    public const string ProductName = "Collimation Circles";
    public const string RequestLicensePage = $"{BasePage}/software/request-license";
    public const string PatreonShop = $"https://www.patreon.com/SaimonsAstronomy/shop/collimation-circles-4-licence-984002";

    public static string GetAppMajorVersion()
    {
        var entryAssembly = Assembly.GetEntryAssembly();

        var assemblyVersion = entryAssembly?.GetName().Version;

        int major = assemblyVersion?.Major ?? 0;

        return $"{major}";
    }
    public static string GetAppName()
    {
        var entryAssembly = Assembly.GetEntryAssembly();

        var assemblyVersion = entryAssembly?.GetName().Name;

        return assemblyVersion?.ToString() ?? nameof(CollimationCircles);
    }

    public static string GetAppVersion()
    {
        var entryAssembly = Assembly.GetEntryAssembly();

        var assemblyVersion = entryAssembly?.GetName().Version;

        return assemblyVersion?.ToString() ?? "0.0.0";
    }

    public static string GetAppVersionTitle()
    {
        var infoVersion = Assembly.GetExecutingAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        var infoVer = infoVersion?.Split("+")?.FirstOrDefault();

        return infoVer ?? "0.0.0";
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

    public static string Serialize<T>(T obj, Formatting formating = Formatting.None)
    {
        return JsonConvert.SerializeObject(obj, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto,
            Formatting = formating
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

    public static Task<(int, string, Process)> ExecuteCommand(string fileName, List<string> arguments, Action? started = null, int timeout = -1)
    {
        var tcs = new TaskCompletionSource<(int, string, Process)>();

        ProcessStartInfo startInfo = new()
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
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

                    string logMessage = $"Command '{fileName} {argStr}' executed. Return code: {process.ExitCode}, Output: {outputStr}";

                    if (!string.IsNullOrWhiteSpace($"{error}"))
                    {
                        logMessage += $", Error: {error}";
                    }

                    logger.Debug(logMessage);
                    tcs.TrySetResult((process.ExitCode, outputStr, process));
                }
                else
                {
                    // Timed out.
                    logger.Warn($"Timeout '{fileName} {argStr}'");
                    tcs.TrySetResult((-1, string.Empty, process));
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

    public static void OpenUrl(string url)
    {
        logger.Trace($"Opening external url '{url}'");

        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true
            });
        }
        else if (OperatingSystem.IsLinux())
        {
            Process.Start("xdg-open", url);
        }
        else if (OperatingSystem.IsMacOS())
        {
            Process.Start("open", url);
        }

        logger.Trace($"External url '{url}' opened");
    }

    public static string? GetLocalIPAddress()
    {
        using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, 0);
        socket.Connect("8.8.8.8", 65530);
        IPEndPoint? endPoint = socket.LocalEndPoint as IPEndPoint;
        return endPoint?.Address.ToString();
    }

    public static async Task StartRaspberryPIStream(string port, List<string> streamArgs, string command = "rpicam-vid")
    {
        Guard.IsNotNullOrWhiteSpace(port);
        Guard.IsNotNull(streamArgs);

        _ = await ExecuteCommand("pkill", [command], timeout: 100);        

        _ = await ExecuteCommand(
            command,
            streamArgs, timeout: 1500);
    }

    public static string DeviceId()
    {
        return new DeviceIdBuilder()
        .AddMachineName()
        .AddOsVersion()
        .OnWindows(windows => windows
            .AddMotherboardSerialNumber()
            .AddSystemDriveSerialNumber())
        .OnLinux(linux => linux
            .AddMotherboardSerialNumber()
            .AddSystemDriveSerialNumber())
        .OnMac(mac => mac
            .AddSystemDriveSerialNumber()
            .AddPlatformSerialNumber())
        .ToString();
    }

    public static void LogSystemInformation()
    {
        logger.Info($"Application Name: {GetAppName()}, Version: {GetAppVersionTitle()}");
        logger.Info($"OS Version: {RuntimeInformation.OSDescription}");
        logger.Info($"OS Architecture: {RuntimeInformation.OSArchitecture}");
        logger.Info($"Device ID: {DeviceId()}");
    }
}
