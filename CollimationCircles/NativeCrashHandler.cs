using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CollimationCircles
{
    /// <summary>
    /// Installs POSIX signal handlers on Linux to capture native crashes
    /// (SIGSEGV, SIGABRT, SIGFPE, SIGBUS) and log a backtrace before the
    /// process dies.  This is necessary because .NET's
    /// <see cref="AppDomain.UnhandledException"/> does not fire for crashes
    /// that originate inside native code (e.g. libvlc, libASICamera2).
    /// </summary>
    internal static class NativeCrashHandler
    {
        private const int SIGSEGV = 11;
        private const int SIGABRT = 6;
        private const int SIGFPE = 8;
        private const int SIGBUS = 7;

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        [DllImport("libc", SetLastError = true)]
        private static extern int sigaction(int signum, ref Sigaction act, IntPtr oldact);

        [DllImport("libc", SetLastError = true)]
        private static extern int raise(int sig);

        [DllImport("libc", SetLastError = true)]
        private static extern void _exit(int status);

        [DllImport("libgcc_s.so.1", EntryPoint = "_Unwind_Backtrace")]
        private static extern int UnwindBacktrace(UnwindTraceFn callback, IntPtr context);

        [DllImport("libgcc_s.so.1", EntryPoint = "_Unwind_GetIP")]
        private static extern IntPtr UnwindGetIP(IntPtr cursor);

        private delegate int UnwindTraceFn(IntPtr cursor, IntPtr context);

        [StructLayout(LayoutKind.Sequential)]
        private struct Sigaction
        {
            public IntPtr sa_handler;
            public ulong sa_mask;
            public int sa_flags;
            public IntPtr sa_restorer;
        }

        // Keep the delegate alive to prevent GC.
        private static readonly UnwindTraceFn _unwindCallback = UnwindCallback;
        private static int _inHandler;

        public static void Install()
        {
            if (!OperatingSystem.IsLinux())
            {
                return;
            }

            foreach (int sig in new[] { SIGSEGV, SIGABRT, SIGFPE, SIGBUS })
            {
                var act = new Sigaction
                {
                    sa_handler = Marshal.GetFunctionPointerForDelegate((SignalHandler)OnSignal),
                    sa_mask = 0,
                    sa_flags = 0,
                    sa_restorer = IntPtr.Zero
                };

                sigaction(sig, ref act, IntPtr.Zero);
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SignalHandler(int signal);

        private static void OnSignal(int signal)
        {
            // Guard against re-entrancy.
            if (System.Threading.Interlocked.Exchange(ref _inHandler, 1) != 0)
            {
                return;
            }

            string sigName = signal switch
            {
                SIGSEGV => "SIGSEGV",
                SIGABRT => "SIGABRT",
                SIGFPE => "SIGFPE",
                SIGBUS => "SIGBUS",
                _ => $"signal {signal}"
            };

            string msg = $"*** Native crash: {sigName} ***";

            try
            {
                logger.Fatal(msg);
                logger.Fatal("Backtrace (may be incomplete on ARM64):");

                // Attempt a backtrace via libgcc's _Unwind_Backtrace.
                try
                {
                    int count = 0;
                    int Callback(IntPtr cursor, IntPtr ctx)
                    {
                        IntPtr ip = UnwindGetIP(cursor);
                        if (ip != IntPtr.Zero)
                        {
                            logger.Fatal($"  #{count,-2} {ip.ToInt64():x}");
                            count++;
                        }
                        return 0;
                    }

                    UnwindBacktrace(Callback, IntPtr.Zero);
                }
                catch
                {
                    logger.Fatal("  (backtrace unavailable)");
                }

                logger.Fatal("To get a full backtrace, run under gdb:");
                logger.Fatal("  gdb -batch -ex run -ex 'thread apply all bt full' -ex quit --args ./CollimationCircles");

                NLog.LogManager.Shutdown();
            }
            catch
            {
                // Swallow — we're in a signal handler.
            }

            // Also write to stderr.
            try
            {
                Console.Error.WriteLine(msg);
            }
            catch
            {
                // ignore
            }

            // Re-raise the signal with the default handler so a core dump is produced.
            _exit(128 + signal);
        }

        private static int UnwindCallback(IntPtr cursor, IntPtr context)
        {
            IntPtr ip = UnwindGetIP(cursor);
            if (ip != IntPtr.Zero)
            {
                logger.Fatal($"  {ip.ToInt64():x}");
            }
            return 0;
        }
    }
}
