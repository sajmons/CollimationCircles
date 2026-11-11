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
    /// overlay. Supported on Windows 10 (2004+) and macOS; on Linux there is no equivalent for X11 or Wayland,
    /// so the call is a no-op and windows appear in captures.
    /// </summary>
    internal sealed class CaptureExclusion
    {
        private const uint WdaNone = 0x0;
        private const uint WdaExcludeFromCapture = 0x11;

        private const nuint NSWindowSharingNone = 0;
        private const nuint NSWindowSharingReadOnly = 1;

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly HashSet<Window> excludedWindows = [];

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "sel_registerName")]
        private static extern IntPtr SelRegisterName([MarshalAs(UnmanagedType.LPStr)] string name);

        [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
        private static extern void ObjcSendNUInt(IntPtr receiver, IntPtr selector, nuint value);

        private static void SetMacSharingType(IntPtr nsWindow, nuint sharingType)
        {
            ObjcSendNUInt(nsWindow, SelRegisterName("setSharingType:"), sharingType);
        }

        public void Apply(IReadOnlyList<Window> windows)
        {
            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsMacOS())
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

                if (OperatingSystem.IsMacOS())
                {
                    SetMacSharingType(handle.Handle, NSWindowSharingNone);
                }
                else if (!SetWindowDisplayAffinity(handle.Handle, WdaExcludeFromCapture))
                {
                    logger.Warn($"Unable to exclude window '{window.Title}' from screen capture.");
                }

                excludedWindows.Add(window);
            }
        }

        public void Restore()
        {
            if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
            {
                foreach (var window in excludedWindows)
                {
                    if (window.TryGetPlatformHandle() is IPlatformHandle handle && handle.Handle != IntPtr.Zero)
                    {
                        if (OperatingSystem.IsMacOS())
                        {
                            SetMacSharingType(handle.Handle, NSWindowSharingReadOnly);
                        }
                        else
                        {
                            SetWindowDisplayAffinity(handle.Handle, WdaNone);
                        }
                    }
                }
            }

            excludedWindows.Clear();
        }
    }
}
