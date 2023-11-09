using Newtonsoft.Json;
using Octokit;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace CollimationCircles.Services;
public class AppService : IAppService
{
    private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
    private const string stateFile = "appstate.json";
    private readonly GitHubClient client = new(new ProductHeaderValue("CollimationCircles"));
    private readonly string owner = "sajmons";
    private readonly string reponame = "CollimationCircles";

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

        string version = entryAssembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? string.Empty;

        return version;
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

        return $"{os}-{System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}".ToLower() ?? null;
    }
}
