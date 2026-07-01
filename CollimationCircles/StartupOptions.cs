using System;

namespace CollimationCircles
{
    internal static class StartupOptions
    {
        public static string? AutoConnectCameraName { get; private set; }

        public static void Initialize(string[] args)
        {
            AutoConnectCameraName = null;

            if (args is null || args.Length == 0)
            {
                return;
            }

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (string.Equals(arg, "--camera", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length && !string.IsNullOrWhiteSpace(args[i + 1]))
                    {
                        AutoConnectCameraName = args[i + 1].Trim();
                    }

                    continue;
                }

                const string cameraPrefix = "--camera=";
                if (arg.StartsWith(cameraPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string value = arg[cameraPrefix.Length..].Trim();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        AutoConnectCameraName = value;
                    }
                }
            }
        }
    }
}
