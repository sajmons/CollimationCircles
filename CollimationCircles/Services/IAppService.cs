using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    public interface IAppService
    {
        string GetAppVersion();
        T? Deserialize<T>(string jsonState);
        string Serialize<T>(T obj);
        void SaveState<T>(T obj, string? fileName = null);
        T? LoadState<T>(string? fileName = null);
        Task<(string, string)> DownloadUrl(string version);
    }
}
