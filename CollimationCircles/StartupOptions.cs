using System;
using System.Globalization;

namespace CollimationCircles
{
    /// <summary>
    /// Parses command-line options used at startup.
    /// Supported commands:
    /// --camera <name> or --camera=<name>
    /// --camera-vidpid <vid:pid> or --camera-vidpid=<vid:pid>
    /// --recover-uvc <vid:pid> or --recover-uvc=<vid:pid>
    /// </summary>
    internal static class StartupOptions
    {
        /// <summary>
        /// Camera name to auto-connect at startup.
        /// Command: --camera
        /// Example: --camera "ocal4.1"
        /// </summary>
        public static string? AutoConnectCameraName { get; private set; }

        /// <summary>
        /// Camera vendor/product pair to auto-connect at startup.
        /// Command: --camera-vidpid
        /// Example: --camera-vidpid 60324:4867
        /// </summary>
        public static (int VendorId, int ProductId)? AutoConnectCameraVidPid { get; private set; }

        /// <summary>
        /// Vendor/product pair used to run recovery-only mode and restore
        /// the UVC device configuration without launching the UI.
        /// Command: --recover-uvc
        /// Example: --recover-uvc 60324:4867
        /// </summary>
        public static (int VendorId, int ProductId)? RecoverUvcVidPid { get; private set; }

        public static void Initialize(string[] args)
        {
            AutoConnectCameraName = null;
            AutoConnectCameraVidPid = null;
            RecoverUvcVidPid = null;

            if (args is null || args.Length == 0)
            {
                return;
            }

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                // --camera <name>
                if (string.Equals(arg, "--camera", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length && !string.IsNullOrWhiteSpace(args[i + 1]))
                    {
                        AutoConnectCameraName = args[i + 1].Trim();
                    }

                    continue;
                }

                // --camera-vidpid <vid:pid>
                if (string.Equals(arg, "--camera-vidpid", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        AutoConnectCameraVidPid = ParseVidPid(args[i + 1]);
                    }

                    continue;
                }

                // --recover-uvc <vid:pid>
                if (string.Equals(arg, "--recover-uvc", StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length)
                    {
                        RecoverUvcVidPid = ParseVidPid(args[i + 1]);
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

                    continue;
                }

                const string cameraVidPidPrefix = "--camera-vidpid=";
                if (arg.StartsWith(cameraVidPidPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    AutoConnectCameraVidPid = ParseVidPid(arg[cameraVidPidPrefix.Length..]);
                    continue;
                }

                const string recoverUvcPrefix = "--recover-uvc=";
                if (arg.StartsWith(recoverUvcPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    RecoverUvcVidPid = ParseVidPid(arg[recoverUvcPrefix.Length..]);
                }
            }
        }

        private static (int VendorId, int ProductId)? ParseVidPid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string[] parts = value.Trim().Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                return null;
            }

            if (!TryParseId(parts[0], out int vendorId) || !TryParseId(parts[1], out int productId))
            {
                return null;
            }

            if (vendorId <= 0 || productId <= 0)
            {
                return null;
            }

            return (vendorId, productId);
        }

        private static bool TryParseId(string value, out int id)
        {
            value = value.Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id);
            }

            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
        }
    }
}
