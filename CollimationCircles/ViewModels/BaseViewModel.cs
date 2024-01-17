using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Linq;
using System.Reflection;

namespace CollimationCircles.ViewModels
{
    public partial class BaseViewModel : ObservableValidator
    {
        [ObservableProperty]
        public string title = string.Empty;

        [ObservableProperty]
        public string mainTitle = string.Empty;

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
