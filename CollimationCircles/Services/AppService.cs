using Newtonsoft.Json;
using Octokit;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CollimationCircles.Services;
public class AppService : IAppService
{
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
    private const string stateFile = "appstate.json";
    private readonly GitHubClient client = new(new ProductHeaderValue("CollimationCircles"));
    private readonly string owner = "sajmons";
    private readonly string reponame = "CollimationCircles";

    public const string LIBCAMERA_VID = "libcamera-vid";
    public const string LIBCAMERA_APPS = "libcamera-apps";
    public const string LIBVLC = "vlc";
    public const string LIBVLC_DEV = "libvlc-dev";

    public string WebPage => "https://saimons-astronomy.webador.com/software/collimation-circles";
    public string ContactPage => "https://saimons-astronomy.webador.com/about";
    public string GitHubPage => "https://github.com/sajmons/CollimationCircles";
    public string TwitterPage => "https://twitter.com/saimons_art";
    public string YouTubeChannel => "https://www.youtube.com/channel/UCz6iFL9ziUcWgs_n6n2gwZw";
    public string GitHubIssue => "https://github.com/sajmons/CollimationCircles/issues/new";
    public string PatreonWebPage => "https://www.patreon.com/SaimonsAstronomy";

    public string GetAppVersion()
    {
        var entryAssembly = Assembly.GetEntryAssembly();

        var assemblyVersion = entryAssembly?.GetName().Version;

        return assemblyVersion?.ToString() ?? "0.0.0";
    }

    public bool SameVersion(string v1, string v2)
    {
        return new Version(v1) == new Version(v2);
    }

    public T? Deserialize<T>(string jsonState)
    {
        return JsonConvert.DeserializeObject<T>(jsonState,
            new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                NullValueHandling = NullValueHandling.Ignore,
            });
    }

    public string Serialize<T>(T obj)
    {
        return JsonConvert.SerializeObject(obj, new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto
        });
    }

    public T? LoadState<T>(string? fileName = null)
    {
        logger.Info($"Loading application state from '{fileName ?? stateFile}'");

        var jsonState = File.ReadAllText(fileName ?? stateFile);

        return Deserialize<T>(jsonState);
    }

    public void SaveState<T>(T obj, string? fileName = null)
    {
        var jsonState = Serialize<T>(obj);

        File.WriteAllText(fileName ?? stateFile, jsonState, System.Text.Encoding.UTF8);

        logger.Info($"Saving application state to '{fileName ?? stateFile}'");
    }

    public async Task<(bool, string, string)> DownloadUrl(string currentVersion)
    {
        try
        {
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

    public async Task OpenFileBrowser(string path)
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

        using Process folderOpener = new Process();
        folderOpener.StartInfo.FileName = System.IO.Path.GetDirectoryName(path);
        folderOpener.StartInfo.UseShellExecute = true;
        folderOpener.Start();
        await folderOpener.WaitForExitAsync();
    }

    public static Task<bool> IsPackageInstalled(string package)
    {
        var tcs = new TaskCompletionSource<bool>();

        var result = ExecuteCommand("dpkg", $"-s {package} | grep Status");

        if (result.Result.Item2 == "Status: install ok installed")
        {
            tcs.TrySetResult(true);
        }
        else
        { 
            tcs.TrySetResult(false);
        }

        return tcs.Task;
    }

    public static Task<(int, string)> ExecuteCommand(string fileName, string arguments)
    {
        var tcs = new TaskCompletionSource<(int, string)>();

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = fileName,
                Arguments = arguments
            };

            Process proc = new()
            {
                StartInfo = startInfo
            };

            if (proc.Start())
            {                
                proc.OutputDataReceived += delegate (object sender, DataReceivedEventArgs e)
                {
                    logger.Info($"Command '{fileName}{arguments}' executed");
                    tcs.TrySetResult((proc.ExitCode, e.Data ?? string.Empty));
                };
            }
            else
            {
                logger.Info($"Failed to execute command '{fileName}{arguments}'");
                tcs.TrySetResult((proc.ExitCode, string.Empty));
            }
        }
        catch (Exception exc)
        {
            logger.Info($"Failed to execute command '{fileName}{arguments}' '{exc.Message}'");
            tcs.TrySetResult((-1, exc.Message));
        }

        return tcs.Task;
    }

    public static Task<Process> StartProcess(string fileName, string arguments)
    {
        var tcs = new TaskCompletionSource<Process>();

        try
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = fileName,
                Arguments = arguments
            };

            Process proc = new()
            {
                StartInfo = startInfo
            };

            if (proc.Start())
            {
                proc.OutputDataReceived += delegate (object sender, DataReceivedEventArgs e)
                {
                    logger.Info($"Command '{fileName}{arguments}' executed");
                    tcs.TrySetResult(proc);
                };
            }
            else
            {
                tcs.TrySetResult(proc);
            }
        }
        catch (Exception exc)
        {
            tcs.TrySetException(exc);
        }

        return tcs.Task;
    }

    public static Task<Process> StartTCPCameraStream(string address)
    {
        var tcs = new TaskCompletionSource<Process>();

        var result = StartProcess(
            LIBCAMERA_VID, 
            $"-t 0 --inline --nopreview --listen -o tcp://{address}");        

        return tcs.Task;
    }

    public static bool CheckRequirements()
    {
        bool result = true;

        if (OperatingSystem.IsLinux())
        {
            result &= IsPackageInstalled(LIBVLC).GetAwaiter().GetResult();
            result &= IsPackageInstalled(LIBVLC_DEV).GetAwaiter().GetResult();
        }

        return result;
    }
}
