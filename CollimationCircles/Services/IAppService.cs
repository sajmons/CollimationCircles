﻿using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    public interface IAppService
    {
        string WebPage { get; }
        string ContactPage { get; }
        string GitHubPage { get; }
        string TwitterPage { get; }        
        string YouTubeChannel { get; }
        string PatreonWebPage { get; }
        string GitHubIssue { get; }
        string GetAppVersion();
        bool SameVersion(string v1, string v2);
        T? Deserialize<T>(string jsonState);
        string Serialize<T>(T obj);
        void SaveState<T>(T obj, string? fileName = null);
        T? LoadState<T>(string? fileName = null);
        Task<(bool, string, string)> DownloadUrl(string version);
        Task OpenFileBrowser(string path);
    }
}
