using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace CollimationCircles.ViewModels
{
    public partial class BaseViewModel : ObservableValidator
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        [ObservableProperty]
        public string title = string.Empty;

        [ObservableProperty]
        public string mainTitle = string.Empty;

        public static void OpenUrl(string url)
        {
            logger.Info($"Opening external url '{url}'");

            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }

        [RelayCommand]
        public static void Translate(string targetLanguage)
        {
            var translations = Application.Current?.Resources.MergedDictionaries.OfType<ResourceInclude>().FirstOrDefault(x => x.Source?.OriginalString?.Contains("/Lang/") ?? false);

            if (translations != null)
                Application.Current?.Resources.MergedDictionaries.Remove(translations);

            string? assemblyName = Assembly.GetEntryAssembly()?.GetName().Name;

            if (assemblyName is not null)
            {
                var uri = new Uri($"avares://{assemblyName}/Resources/Lang/{targetLanguage}.axaml");

                Application.Current?.Resources.MergedDictionaries.Add(
                    new ResourceInclude(uri)
                    {
                        Source = uri
                    }
                );
            }
        }        
    }
}
