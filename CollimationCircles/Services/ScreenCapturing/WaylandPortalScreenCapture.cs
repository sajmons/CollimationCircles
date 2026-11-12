using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Tmds.DBus.Protocol;

namespace CollimationCircles.Services.ScreenCapturing
{
    /// <summary>
    /// Captures the screen in Wayland sessions with the xdg-desktop-portal Screenshot API.
    /// The portal only hands out a PNG file, so each capture is decoded in memory, cropped and the file is deleted.
    /// The first capture shows a permission prompt from the desktop environment.
    /// </summary>
    internal sealed class WaylandPortalScreenCapture : IScreenCaptureBackend
    {
        private const string PortalService = "org.freedesktop.portal.Desktop";
        private const string PortalPath = "/org/freedesktop/portal/desktop";
        private const string ScreenshotInterface = "org.freedesktop.portal.Screenshot";
        private const string RequestInterface = "org.freedesktop.portal.Request";

        private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(60);
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public string Name => "Wayland desktop portal";

        public ScreenCaptureFrame Capture(CaptureRegion region)
        {
            var uri = RequestScreenshotAsync().GetAwaiter().GetResult();
            var path = new Uri(uri).LocalPath;

            byte[] png;
            try
            {
                png = File.ReadAllBytes(path);
            }
            finally
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex)
                {
                    logger.Warn($"Unable to delete portal screenshot '{path}': {ex.Message}");
                }
            }

            return Crop(png, region);
        }

        private static ScreenCaptureFrame Crop(byte[] png, CaptureRegion region)
        {
            using var full = Cv2.ImDecode(png, ImreadModes.Color);
            if (full.Empty())
            {
                throw new InvalidOperationException("The portal screenshot could not be decoded.");
            }

            var bounds = region.Bounds;
            var left = Math.Max(0, bounds.X);
            var top = Math.Max(0, bounds.Y);
            var width = Math.Min(full.Width, bounds.Right) - left;
            var height = Math.Min(full.Height, bounds.Bottom) - top;
            if (width <= 0 || height <= 0)
            {
                throw new InvalidOperationException("The capture area is outside of the screenshot.");
            }

            using var cropped = new Mat(full, new Rect(left, top, width, height));
            using var bgra = new Mat();
            Cv2.CvtColor(cropped, bgra, ColorConversionCodes.BGR2BGRA);

            var stride = width * 4;
            var pixels = new byte[checked(stride * height)];
            Marshal.Copy(bgra.Data, pixels, 0, pixels.Length);
            return new ScreenCaptureFrame(pixels, width, height, stride);
        }

        private static async Task<string> RequestScreenshotAsync()
        {
            using var connection = new DBusConnection(DBusAddress.Session
                ?? throw new InvalidOperationException("The D-Bus session bus is unavailable."));
            await connection.ConnectAsync();

            // The portal answers asynchronously with a Response signal on a request path derived from this token.
            var token = "cc_" + Guid.NewGuid().ToString("N");
            var sender = (connection.UniqueName ?? throw new InvalidOperationException("No D-Bus unique name."))
                .TrimStart(':').Replace('.', '_');
            var requestPath = $"/org/freedesktop/portal/desktop/request/{sender}/{token}";

            var response = new TaskCompletionSource<(uint Code, string? Uri)>(TaskCreationOptions.RunContinuationsAsynchronously);

            var rule = new MatchRule
            {
                Type = MessageType.Signal,
                Interface = RequestInterface,
                Member = "Response",
                Path = requestPath
            };

            // The signal must be subscribed before the call, otherwise a fast response can be missed.
            using var subscription = await connection.AddMatchAsync(
                rule,
                (Message message, object? state) => ReadResponse(message),
                (Notification<(uint Code, string? Uri)> notification) =>
                {
                    if (notification.HasValue)
                    {
                        response.TrySetResult(notification.Value);
                    }
                    else
                    {
                        response.TrySetException(notification.Exception);
                    }
                },
                emitOnCapturedContext: false,
                flags: ObserverFlags.None);

            await connection.CallMethodAsync(CreateScreenshotCall(connection, token));

            if (await Task.WhenAny(response.Task, Task.Delay(ResponseTimeout)) != response.Task)
            {
                throw new TimeoutException("The desktop portal did not answer the screenshot request.");
            }

            var (code, uri) = await response.Task;
            return code switch
            {
                0 when !string.IsNullOrEmpty(uri) => uri,
                0 => throw new InvalidOperationException("The desktop portal returned no screenshot."),
                1 => throw new InvalidOperationException("The screenshot permission was denied or cancelled."),
                _ => throw new InvalidOperationException($"The desktop portal failed to take a screenshot (code {code}).")
            };
        }

        private static MessageBuffer CreateScreenshotCall(DBusConnection connection, string token)
        {
            using var writer = connection.GetMessageWriter();

            writer.WriteMethodCallHeader(
                destination: PortalService,
                path: PortalPath,
                @interface: ScreenshotInterface,
                signature: "sa{sv}",
                member: "Screenshot");

            writer.WriteString(string.Empty);
            writer.WriteDictionary(new Dictionary<string, VariantValue>
            {
                ["handle_token"] = token,
                ["interactive"] = false
            });

            return writer.CreateMessage();
        }

        private static (uint Code, string? Uri) ReadResponse(Message message)
        {
            var reader = message.GetBodyReader();
            var code = reader.ReadUInt32();
            var results = reader.ReadDictionaryOfStringToVariantValue();

            string? uri = null;
            if (results.TryGetValue("uri", out var value))
            {
                uri = value.GetString();
            }

            return (code, uri);
        }
    }
}
