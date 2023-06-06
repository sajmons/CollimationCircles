using Avalonia;

namespace CollimationCircles.Helper
{
    public static class DynRes
    {
        public static string TryGetString(string resourceKey)
        {
            try
            {
                string? value = Application.Current?.Resources[resourceKey] as string;

                return value ?? "Undefined";
            }
            catch
            {
                return "Undefined";
            }
        }
    }
}
