using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace CollimationCircles.Services.ScreenCapturing
{
    /// <summary>The desktop portal cannot provide a screen cast, so another capture method should be tried.</summary>
    internal sealed class ScreenCastUnavailableException(string message, Exception? innerException = null)
        : InvalidOperationException(message, innerException);

    /// <summary>
    /// Captures the screen in Wayland sessions with the xdg-desktop-portal ScreenCast API.
    /// The user approves screen sharing once when capture starts, after which frames arrive continuously
    /// from PipeWire without any further prompts or shutter sounds. PipeWire frames are read with a
    /// gst-launch-1.0 child process that is handed the portal's PipeWire connection and writes raw BGRA frames to a pipe.
    /// </summary>
    internal sealed class WaylandPortalScreenCapture : IScreenCaptureBackend, IDisposable
    {
        private const string PortalService = "org.freedesktop.portal.Desktop";
        private const string PortalPath = "/org/freedesktop/portal/desktop";
        private const string ScreenCastInterface = "org.freedesktop.portal.ScreenCast";
        private const string RequestInterface = "org.freedesktop.portal.Request";

        private const uint SourceTypeMonitor = 1;
        private const uint CursorModeHidden = 1;
        private const uint PersistUntilRevoked = 2;
        private const int FcntlSetFd = 2;

        private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(120);
        private static readonly TimeSpan FirstFrameTimeout = TimeSpan.FromSeconds(15);
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly CancellationTokenSource lifetime = new();
        private readonly object sync = new();
        private readonly StringBuilder gstErrors = new();

        private DBusConnection? connection;
        private Process? gst;
        private byte[]? latest;
        private int frameWidth;
        private int frameHeight;
        private Exception? readerError;

        private delegate void BodyWriter(ref MessageWriter writer);

        private sealed record StreamInfo(uint NodeId, int Width, int Height);

        private sealed record PortalResponse(uint Code, string? SessionHandle, string? RestoreToken, List<StreamInfo> Streams);

        [DllImport("libc", SetLastError = true)]
        private static extern int fcntl(int fd, int cmd, int arg);

        public string Name => "Wayland screen cast portal";

        public ScreenCaptureFrame Capture(CaptureRegion region)
        {
            ThrowIfStopped();

            if (gst is null)
            {
                StartAsync(region).GetAwaiter().GetResult();
            }

            return CropLatest(region);
        }

        public void Dispose()
        {
            if (lifetime.IsCancellationRequested)
            {
                return;
            }

            lifetime.Cancel();

            try
            {
                if (gst is { HasExited: false })
                {
                    gst.Kill(true);
                }
            }
            catch (Exception ex)
            {
                logger.Warn($"Unable to stop gst-launch: {ex.Message}");
            }

            gst?.Dispose();

            // Closing the D-Bus connection also ends the portal session and the screen sharing indicator.
            connection?.Dispose();

            lock (sync)
            {
                Monitor.PulseAll(sync);
            }
        }

        private void ThrowIfStopped()
        {
            if (lifetime.IsCancellationRequested)
            {
                throw new OperationCanceledException();
            }
        }

        private async Task StartAsync(CaptureRegion region)
        {
            var bus = new DBusConnection(DBusAddress.Session
                ?? throw new InvalidOperationException("The D-Bus session bus is unavailable."));
            connection = bus;
            await bus.ConnectAsync();

            var created = await RequestAsync(bus, token => BuildCall(bus, "CreateSession", "a{sv}", (ref MessageWriter w) =>
                w.WriteDictionary(new Dictionary<string, VariantValue>
                {
                    ["handle_token"] = token,
                    ["session_handle_token"] = "cc_session_" + Guid.NewGuid().ToString("N")
                })));
            EnsureAccepted(created);

            var session = created.SessionHandle
                ?? throw new InvalidOperationException("The desktop portal returned no screen cast session.");

            var selected = await RequestAsync(bus, token => BuildCall(bus, "SelectSources", "oa{sv}", (ref MessageWriter w) =>
            {
                w.WriteObjectPath(session);

                var options = new Dictionary<string, VariantValue>
                {
                    ["handle_token"] = token,
                    ["types"] = SourceTypeMonitor,
                    ["multiple"] = false,
                    ["cursor_mode"] = CursorModeHidden,
                    ["persist_mode"] = PersistUntilRevoked
                };

                var restoreToken = LoadRestoreToken();
                if (!string.IsNullOrEmpty(restoreToken))
                {
                    options["restore_token"] = restoreToken;
                }

                w.WriteDictionary(options);
            }));
            EnsureAccepted(selected);

            logger.Info("Waiting for the screen sharing selection in the desktop portal dialog");

            var started = await RequestAsync(bus, token => BuildCall(bus, "Start", "osa{sv}", (ref MessageWriter w) =>
            {
                w.WriteObjectPath(session);
                w.WriteString(string.Empty);
                w.WriteDictionary(new Dictionary<string, VariantValue> { ["handle_token"] = token });
            }));
            EnsureAccepted(started);

            if (!string.IsNullOrEmpty(started.RestoreToken))
            {
                SaveRestoreToken(started.RestoreToken);
            }

            var stream = started.Streams.Count > 0
                ? started.Streams[0]
                : throw new InvalidOperationException("The desktop portal did not provide a screen stream.");

            var width = stream.Width > 0 ? stream.Width : region.MonitorBounds.Width;
            var height = stream.Height > 0 ? stream.Height : region.MonitorBounds.Height;
            if (width <= 0 || height <= 0)
            {
                throw new InvalidOperationException("The size of the shared screen is unknown.");
            }

            var remote = await bus.CallMethodAsync(
                BuildCall(bus, "OpenPipeWireRemote", "oa{sv}", (ref MessageWriter w) =>
                {
                    w.WriteObjectPath(session);
                    w.WriteDictionary(new Dictionary<string, VariantValue>());
                }),
                (Message message, object? state) => message.GetBodyReader().ReadHandle<SafeFileHandle>(),
                null);

            ThrowIfStopped();
            StartGstreamer(remote, stream.NodeId, width, height);
            logger.Info($"Screen cast started (PipeWire node {stream.NodeId}, {width}x{height})");
        }

        private void StartGstreamer(SafeFileHandle remote, uint nodeId, int width, int height)
        {
            using (remote)
            {
                var fd = (int)remote.DangerousGetHandle();

                // Received file descriptors are close-on-exec, but the child process must inherit this one.
                if (fcntl(fd, FcntlSetFd, 0) != 0)
                {
                    throw new InvalidOperationException($"Unable to share the PipeWire connection (errno {Marshal.GetLastWin32Error()}).");
                }

                var startInfo = new ProcessStartInfo("gst-launch-1.0")
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // -q keeps gst-launch from printing status text into the pipe that carries the frames.
                foreach (var argument in new[]
                {
                    "-q",
                    "pipewiresrc", $"fd={fd}", $"path={nodeId}",
                    "!", "videoconvert",
                    "!", "videoscale",
                    "!", $"video/x-raw,format=BGRA,width={width},height={height}",
                    "!", "fdsink", "fd=1"
                })
                {
                    startInfo.ArgumentList.Add(argument);
                }

                Process process;
                try
                {
                    process = Process.Start(startInfo)
                        ?? throw new InvalidOperationException("gst-launch-1.0 could not be started.");
                }
                catch (Win32Exception ex)
                {
                    throw new ScreenCastUnavailableException(
                        "Continuous screen capture on Wayland needs GStreamer with PipeWire support. " +
                        "Install it with: sudo apt install gstreamer1.0-tools gstreamer1.0-pipewire", ex);
                }

                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data is not null)
                    {
                        lock (gstErrors)
                        {
                            if (gstErrors.Length < 4000)
                            {
                                gstErrors.AppendLine(e.Data);
                            }
                        }
                    }
                };
                process.BeginErrorReadLine();

                gst = process;

                var reader = new Thread(() => ReadFrames(process, width, height))
                {
                    IsBackground = true,
                    Name = "Wayland screen cast reader"
                };
                reader.Start();
            }
        }

        private void ReadFrames(Process process, int width, int height)
        {
            try
            {
                var size = checked(width * height * 4);
                var back = new byte[size];
                var stream = process.StandardOutput.BaseStream;

                while (!lifetime.IsCancellationRequested)
                {
                    stream.ReadExactly(back);

                    lock (sync)
                    {
                        (latest, back) = (back, latest ?? new byte[size]);
                        frameWidth = width;
                        frameHeight = height;
                        Monitor.PulseAll(sync);
                    }
                }
            }
            catch (Exception ex)
            {
                if (lifetime.IsCancellationRequested)
                {
                    return;
                }

                string details;
                lock (gstErrors)
                {
                    details = gstErrors.ToString().Trim();
                }

                var message = $"The screen cast stopped: {ex.Message}" + (details.Length > 0 ? $" {details}" : string.Empty);
                lock (sync)
                {
                    readerError = new InvalidOperationException(message, ex);
                    Monitor.PulseAll(sync);
                }
            }
        }

        private ScreenCaptureFrame CropLatest(CaptureRegion region)
        {
            var deadline = DateTime.UtcNow + FirstFrameTimeout;

            lock (sync)
            {
                while (true)
                {
                    ThrowIfStopped();

                    if (readerError is not null)
                    {
                        throw readerError;
                    }

                    if (latest is not null)
                    {
                        break;
                    }

                    if (DateTime.UtcNow > deadline)
                    {
                        throw new TimeoutException("No frame arrived from the screen cast.");
                    }

                    Monitor.Wait(sync, 200);
                }

                // The stream covers the monitor the user shared; its resolution can differ from the window pixels.
                var monitor = region.MonitorBounds;
                var scaleX = frameWidth / (double)Math.Max(1, monitor.Width);
                var scaleY = frameHeight / (double)Math.Max(1, monitor.Height);

                var left = Math.Max(0, (int)Math.Round((region.Bounds.X - monitor.X) * scaleX));
                var top = Math.Max(0, (int)Math.Round((region.Bounds.Y - monitor.Y) * scaleY));
                var right = Math.Min(frameWidth, (int)Math.Round((region.Bounds.Right - monitor.X) * scaleX));
                var bottom = Math.Min(frameHeight, (int)Math.Round((region.Bounds.Bottom - monitor.Y) * scaleY));

                var width = right - left;
                var height = bottom - top;
                if (width <= 0 || height <= 0)
                {
                    throw new InvalidOperationException("The capture area is outside of the shared screen.");
                }

                var stride = width * 4;
                var pixels = new byte[checked(stride * height)];
                for (var y = 0; y < height; y++)
                {
                    Buffer.BlockCopy(latest, ((top + y) * frameWidth + left) * 4, pixels, y * stride, stride);
                }

                return new ScreenCaptureFrame(pixels, width, height, stride);
            }
        }

        private async Task<PortalResponse> RequestAsync(DBusConnection bus, Func<string, MessageBuffer> createCall)
        {
            // The portal answers asynchronously with a Response signal on a request path derived from this token.
            var token = "cc_" + Guid.NewGuid().ToString("N");
            var sender = (bus.UniqueName ?? throw new InvalidOperationException("No D-Bus unique name."))
                .TrimStart(':').Replace('.', '_');
            var requestPath = $"/org/freedesktop/portal/desktop/request/{sender}/{token}";

            var response = new TaskCompletionSource<PortalResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

            var rule = new MatchRule
            {
                Type = MessageType.Signal,
                Interface = RequestInterface,
                Member = "Response",
                Path = requestPath
            };

            // The signal must be subscribed before the call, otherwise a fast response can be missed.
            using var subscription = await bus.AddMatchAsync(
                rule,
                (Message message, object? state) => ReadResponse(message),
                (Notification<PortalResponse> notification) =>
                {
                    if (notification.HasValue)
                    {
                        response.TrySetResult(notification.Value);
                    }
                    else
                    {
                        response.TrySetException(notification.CompletionReason);
                    }
                },
                emitOnCapturedContext: false,
                flags: ObserverFlags.None);

            try
            {
                await bus.CallMethodAsync(createCall(token));
            }
            catch (DBusErrorReplyException ex) when (
                ex.ErrorName is "org.freedesktop.DBus.Error.UnknownMethod"
                    or "org.freedesktop.DBus.Error.UnknownInterface"
                    or "org.freedesktop.DBus.Error.ServiceUnknown")
            {
                // The portal frontend only exposes ScreenCast when a desktop backend implements it.
                throw new ScreenCastUnavailableException(
                    "The desktop portal does not support screen casting. Install the xdg-desktop-portal backend for your desktop " +
                    "(xdg-desktop-portal-gnome, xdg-desktop-portal-kde or xdg-desktop-portal-wlr), " +
                    "make sure it is running in this session and restart the application.", ex);
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            if (await Task.WhenAny(response.Task, Task.Delay(ResponseTimeout, timeout.Token)) != response.Task)
            {
                ThrowIfStopped();
                throw new TimeoutException("The desktop portal did not answer the screen cast request.");
            }

            timeout.Cancel();
            return await response.Task;
        }

        private static void EnsureAccepted(PortalResponse response)
        {
            switch (response.Code)
            {
                case 0:
                    return;
                case 1:
                    throw new InvalidOperationException("Screen sharing was denied or cancelled.");
                default:
                    throw new InvalidOperationException($"The desktop portal failed to start the screen cast (code {response.Code}).");
            }
        }

        private static MessageBuffer BuildCall(DBusConnection bus, string member, string signature, BodyWriter body)
        {
            var writer = bus.GetMessageWriter();
            try
            {
                writer.WriteMethodCallHeader(
                    destination: PortalService,
                    path: PortalPath,
                    @interface: ScreenCastInterface,
                    signature: signature,
                    member: member);

                body(ref writer);
                return writer.CreateMessage();
            }
            finally
            {
                writer.Dispose();
            }
        }

        private static PortalResponse ReadResponse(Message message)
        {
            var reader = message.GetBodyReader();
            var code = reader.ReadUInt32();
            var results = reader.ReadDictionaryOfStringToVariantValue();

            string? session = null;
            if (results.TryGetValue("session_handle", out var sessionValue))
            {
                session = sessionValue.Type == VariantValueType.ObjectPath
                    ? sessionValue.GetObjectPathAsString()
                    : sessionValue.GetString();
            }

            string? restoreToken = null;
            if (results.TryGetValue("restore_token", out var tokenValue))
            {
                restoreToken = tokenValue.GetString();
            }

            var streams = new List<StreamInfo>();
            if (results.TryGetValue("streams", out var streamsValue))
            {
                for (var i = 0; i < streamsValue.Count; i++)
                {
                    var item = streamsValue.GetItem(i);
                    var properties = item.GetItem(1).GetDictionary<string, VariantValue>();

                    int width = 0, height = 0;
                    if (properties.TryGetValue("size", out var size))
                    {
                        width = size.GetItem(0).GetInt32();
                        height = size.GetItem(1).GetInt32();
                    }

                    streams.Add(new StreamInfo(item.GetItem(0).GetUInt32(), width, height));
                }
            }

            return new PortalResponse(code, session, restoreToken, streams);
        }

        private static string RestoreTokenPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "CollimationCircles",
            "screencast-restore-token.txt");

        private static string? LoadRestoreToken()
        {
            try
            {
                return File.Exists(RestoreTokenPath) ? File.ReadAllText(RestoreTokenPath).Trim() : null;
            }
            catch (Exception ex)
            {
                logger.Warn($"Unable to read the screen cast restore token: {ex.Message}");
                return null;
            }
        }

        private static void SaveRestoreToken(string token)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(RestoreTokenPath)!);
                File.WriteAllText(RestoreTokenPath, token);
            }
            catch (Exception ex)
            {
                logger.Warn($"Unable to save the screen cast restore token: {ex.Message}");
            }
        }
    }
}
