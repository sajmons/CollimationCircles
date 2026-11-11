using Avalonia.Controls;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace CollimationCircles.Services.ScreenCapturing
{
    /// <summary>
    /// Hides application windows from screen capture so the content behind them is captured instead of our own
    /// overlay. Only Windows 10 (2004+) offers this; elsewhere the call is a no-op and windows appear in captures.
    /// </summary>
    internal sealed class CaptureExclusion
    {
        private const uint WdaNone = 0x0;
        private const uint WdaExcludeFromCapture = 0x11;

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly HashSet<Window> excludedWindows = [];

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        public void Apply(IReadOnlyList<Window> windows)
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            excludedWindows.RemoveWhere(w => !windows.Contains(w));

            foreach (var window in windows)
            {
                if (excludedWindows.Contains(window)
                    || window.TryGetPlatformHandle() is not IPlatformHandle handle
                    || handle.Handle == IntPtr.Zero)
                {
                    continue;
                }

                if (!SetWindowDisplayAffinity(handle.Handle, WdaExcludeFromCapture))
                {
                    logger.Warn($"Unable to exclude window '{window.Title}' from screen capture.");
                }

                excludedWindows.Add(window);
            }
        }

        public void Restore()
        {
            if (OperatingSystem.IsWindows())
            {
                foreach (var window in excludedWindows)
                {
                    if (window.TryGetPlatformHandle() is IPlatformHandle handle && handle.Handle != IntPtr.Zero)
                    {
                        SetWindowDisplayAffinity(handle.Handle, WdaNone);
                    }
                }
            }

            excludedWindows.Clear();
        }
    }
}
