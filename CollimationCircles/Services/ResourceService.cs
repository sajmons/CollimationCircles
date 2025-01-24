using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using System;
using System.Linq;

namespace CollimationCircles.Services
{
    public class ResourceService : IResourceService
    {
        private ResourceInclude? resources;
        private readonly string textPresset;
        private readonly string langDir;

        public ResourceService(string langDir, string textPresset = "Text")
        { 
            this.langDir = langDir;
            this.textPresset = textPresset;

            Translate();
        }

        public void Translate(string language = "en-US")
        {
            if (resources != null)
                Application.Current?.Resources.MergedDictionaries.Remove(resources);

            var uri = new Uri($"avares://{langDir}/{language}.axaml");

            Application.Current?.Resources.MergedDictionaries.Add(
                new ResourceInclude(uri)
                {
                    Source = uri
                }
            );

            resources = Application.Current?.Resources.MergedDictionaries.OfType<ResourceInclude>()
                .FirstOrDefault(x => x.Source?.OriginalString?.Contains(language) ?? false);
        }

        public string TryGetString(string resourceKey)
        {
            if (resources is null)
            {
                throw new Exception($"Translations are not available.");
            }

            string key = $"{textPresset}.{resourceKey}";

            if (resources.TryGetResource($"{key}", ThemeVariant.Dark, out object? value))
            {
                if (value is null)
                    throw new Exception($"Resource key '{key}' not found.");

                return (string)value;
            }
            else
            {
                throw new Exception($"Resource key '{key}' not found.");
            }
        }

        public string TryGet(string resourceKey)
        {
            if (Application.Current?.Resources.TryGetResource(resourceKey, ThemeVariant.Dark, out object? value) == true)
            {
                return $"{value ?? string.Empty}";
            }
            else
            {
                return string.Empty;
            }
        }
    }
}
