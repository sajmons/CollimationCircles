using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    public interface IAppService
    {
        string GetAppVersion();
        bool SameVersion(string v1, string v2);
        T? Deserialize<T>(string jsonState);
        string Serialize<T>(T obj);
        void SaveState<T>(T obj, string? fileName = null);
        T? LoadState<T>(string? fileName = null);
        Task<(bool, string, string)> DownloadUrl(string version);
    }
}
