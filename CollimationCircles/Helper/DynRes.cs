using Avalonia;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using System;
using System.Linq;

namespace CollimationCircles.Helper
{
    public static class DynRes
    {
        private static ResourceInclude? translations;

        public static string TryGetString(string resourceKey)
        {
            translations ??= Application.Current?.Resources.MergedDictionaries.OfType<ResourceInclude>()
                .FirstOrDefault(x => x.Source?.OriginalString?.Contains("/Lang/") ?? false);

            if (translations is null)
            {
                throw new Exception("Missing resource");
            }

            if (translations.TryGetResource($"Text.{resourceKey}", ThemeVariant.Dark, out object? value))
            {
                if (value is null)
                    throw new Exception($"Resource key '{resourceKey}' not found.");

                return (string)value;
            }
            else
            {
                throw new Exception($"Resource key '{resourceKey}' not found.");
            }
        }

        public static string TryGet(string resourceKey)
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
