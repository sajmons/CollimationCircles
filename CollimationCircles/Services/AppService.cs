using System.Diagnostics;
using System.Reflection;

namespace CollimationCircles.Services
{
    public class AppService : IAppService
    {
        public string GetAppVersion()
        {
            return FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion ?? string.Empty;
        }
    }
}
