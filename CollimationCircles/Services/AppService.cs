using Newtonsoft.Json;
using Octokit;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace CollimationCircles.Services;
public class AppService : IAppService
{
    private const string stateFile = "appstate.json";
    private readonly GitHubClient client = new(new ProductHeaderValue("CollimationCircles"));
    private readonly string owner = "sajmons";
    private readonly string reponame = "CollimationCircles";

    public string GetAppVersion()
    {
        return FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion ?? string.Empty;
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
        var jsonState = File.ReadAllText(fileName ?? stateFile);

        return Deserialize<T>(jsonState);
    }

    public void SaveState<T>(T obj, string? fileName = null)
    {
        var jsonState = Serialize<T>(obj);

        File.WriteAllText(fileName ?? stateFile, jsonState, System.Text.Encoding.UTF8);
    }

    public async Task<(string, string)> DownloadUrl(string currentVersion)
    {
        var release = await client.Repository.Release.GetLatest(owner, reponame);

        var gitHubVer = release.TagName.Split('-')[1];

        Version oldVersion = new Version(currentVersion);

        Version newVersion = new Version(gitHubVer);

        if (newVersion > oldVersion)
            return (release.Assets[0].BrowserDownloadUrl, newVersion.ToString());
        else
            return (string.Empty, string.Empty);
    }    
}
