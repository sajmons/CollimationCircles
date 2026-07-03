using CollimationCircles.Helper.RpiCameraTools;
using CollimationCircles.Messages;
using CollimationCircles.Models;
using Avalonia.Threading;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using LibVLCSharp.Shared;
using Octokit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    internal class LibVLCService : ILibVLCService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private static readonly string[] MacArm64LibVlcSearchPaths =
        [
            "/opt/homebrew/opt/vlc/lib",
            "/opt/homebrew/lib",
            "/usr/local/lib"
        ];
        private static readonly List<nint> NativeLibraryHandles = [];

        private LibVLC? libVLC;
        private ZWOLiveStreamService? _zwoStream;

        private string protocol = string.Empty;
        private string address = string.Empty;
        private string port = string.Empty;
        private string pathAndQuery = string.Empty;
        private const string rpiPort = RasPiCameraDetect.StreamPort;

        public const string SnapshotImageFile = "snapshot.jpg";

        public bool IsAvailable { get; private set; }
        public string FullAddress { get; set; } = string.Empty;
        public MediaPlayer? MediaPlayer { get; private set; }

        private bool _initialized;

        public LibVLCService()
        {
            // Initialization is deferred to first use (see EnsureInitialized) so that a
            // crash inside the native libvlc library (e.g. on Linux ARM64) does not take
            // down the whole application at startup.
        }

        /// <summary>
        /// Lazily creates the LibVLC + MediaPlayer instances on first use.
        /// Returns false if LibVLC could not be initialised (missing/incompatible native
        /// libraries, native crash guard, etc.).  All public methods that need LibVLC
        /// must call this first.
        /// </summary>
        private bool EnsureInitialized()
        {
            if (_initialized)
            {
                return IsAvailable;
            }

            _initialized = true;

            // https://wiki.videolan.org/VLC_command-line_help/
            //
            // NOTE: --verbose=3 was previously used but it triggers a native crash
            // (SIGSEGV inside vsnprintf) on Linux ARM64 with VLC 3.0.x from Debian
            // trixie.  The verbose logging code path corrupts memory when formatting
            // the very long ./configure banner string on that platform.  We keep
            // verbosity at 0 (default) and rely on the managed Log callback below for
            // the messages we actually care about.
            string[] libVLCOptions = [
                // "--verbose=3",
                "--no-snapshot-preview",
                "--no-osd",
                "--no-video-title-show",
                "--avcodec-hw=none", // Disables hardware video decoding flags that crash some ARM64 drivers
                "--no-media-library",
                "--no-stats"
            ];

            if (TryInitializeMacArm64LibVlc(libVLCOptions, out LibVLC? arm64LibVlc, out MediaPlayer? arm64MediaPlayer))
            {
                libVLC = arm64LibVlc;
                MediaPlayer = arm64MediaPlayer;

                if (MediaPlayer is not null)
                {
                    MediaPlayer.Opening += (sender, e) => SendCameraStateOnUIThread(CameraState.Opening);
                    MediaPlayer.Playing += (sender, e) => SendCameraStateOnUIThread(CameraState.Playing);
                    MediaPlayer.Paused += (sender, e) => SendCameraStateOnUIThread(CameraState.Paused);
                    MediaPlayer.Stopped += (sender, e) =>
                    {
                        SendCameraStateOnUIThread(CameraState.Stopped);
                        StopZwoStream();
                    };
                }

                IsAvailable = true;
                logger.Info("LibVLC initialized from macOS arm64 fallback path.");
                return true;
            }

            try
            {
                libVLC = new(libVLCOptions);

                libVLC.Log += (sender, e) =>
                {
                    switch (e.Level)
                    {
                        case LogLevel.Error:
                            logger.Error($"LibVLC: {e.Module} {e.Message}");
                            break;
                        case LogLevel.Debug:
                            logger.Debug($"LibVLC: {e.Module} {e.Message}");
                            break;
                        case LogLevel.Warning:
                            logger.Warn($"LibVLC: {e.Module} {e.Message}");
                            break;
                        case LogLevel.Notice:
                            logger.Info($"LibVLC: {e.Module} {e.Message}");
                            break;
                    }
                };

                MediaPlayer = new(libVLC)
                {
                    FileCaching = 0,
                    NetworkCaching = 0,
                    EnableHardwareDecoding = true
                };

                MediaPlayer.Opening += (sender, e) => SendCameraStateOnUIThread(CameraState.Opening);
                MediaPlayer.Playing += (sender, e) => SendCameraStateOnUIThread(CameraState.Playing);
                MediaPlayer.Paused += (sender, e) => SendCameraStateOnUIThread(CameraState.Paused);
                MediaPlayer.Stopped += (sender, e) =>
                {
                    SendCameraStateOnUIThread(CameraState.Stopped);
                    StopZwoStream();
                };

                IsAvailable = true;
                logger.Info("LibVLC initialized.");
                return true;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Default LibVLC loading failed.");
                IsAvailable = false;
                logger.Error(ex, "LibVLC disabled.");
                return false;
            }
        }

        private static void SendCameraStateOnUIThread(CameraState state)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                WeakReferenceMessenger.Default.Send(new CameraStateMessage(state));
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                WeakReferenceMessenger.Default.Send(new CameraStateMessage(state));
            });
        }

        private static bool TryInitializeMacArm64LibVlc(string[] libVLCOptions, out LibVLC? fallbackLibVlc, out MediaPlayer? fallbackMediaPlayer)
        {
            fallbackLibVlc = null;
            fallbackMediaPlayer = null;

            if (!OperatingSystem.IsMacOS() || RuntimeInformation.ProcessArchitecture != Architecture.Arm64)
            {
                return false;
            }

            foreach (string searchPath in GetMacArm64LibVlcCandidatePaths())
            {
                string dylibPath = Path.Combine(searchPath, "libvlc.dylib");

                if (!File.Exists(dylibPath))
                {
                    continue;
                }

                try
                {
                    if (!TryPreloadMacArm64Libraries(searchPath, out string preloadError))
                    {
                        logger.Warn($"LibVLC preload failed for '{searchPath}': {preloadError}");
                        continue;
                    }

                    string? pluginPath = TryGetPluginPathFromLibPath(searchPath);
                    string? dataPath = TryGetDataPathFromLibPath(searchPath);

                    if (!string.IsNullOrWhiteSpace(pluginPath))
                    {
                        Environment.SetEnvironmentVariable("VLC_PLUGIN_PATH", pluginPath);
                    }

                    if (!string.IsNullOrWhiteSpace(dataPath))
                    {
                        Environment.SetEnvironmentVariable("VLC_DATA_PATH", dataPath);
                    }

                    MergePathEnvironment("DYLD_LIBRARY_PATH", searchPath);
                    MergePathEnvironment("DYLD_FALLBACK_LIBRARY_PATH", searchPath);

                    Core.Initialize(searchPath);

                    fallbackLibVlc = new LibVLC(libVLCOptions);
                    fallbackMediaPlayer = new MediaPlayer(fallbackLibVlc)
                    {
                        FileCaching = 0,
                        NetworkCaching = 0,
                        EnableHardwareDecoding = true
                    };

                    return true;
                }
                catch (Exception ex)
                {
                    logger.Warn($"LibVLC fallback init failed for '{searchPath}': {ex.Message}");
                }
            }

            return false;
        }

        private static bool TryPreloadMacArm64Libraries(string libPath, out string error)
        {
            error = string.Empty;

            string corePath = Path.Combine(libPath, "libvlccore.dylib");
            string vlcPath = Path.Combine(libPath, "libvlc.dylib");

            if (!File.Exists(corePath) || !File.Exists(vlcPath))
            {
                error = "Required files libvlc.dylib/libvlccore.dylib are missing.";
                return false;
            }

            if (!NativeLibrary.TryLoad(corePath, out nint coreHandle))
            {
                error = $"Could not load '{corePath}'.";
                return false;
            }

            if (!NativeLibrary.TryLoad(vlcPath, out nint vlcHandle))
            {
                error = $"Could not load '{vlcPath}'.";
                return false;
            }

            NativeLibraryHandles.Add(coreHandle);
            NativeLibraryHandles.Add(vlcHandle);

            return true;
        }

        private static string? TryGetPluginPathFromLibPath(string libPath)
        {
            try
            {
                var libDirInfo = new DirectoryInfo(libPath);
                if (!libDirInfo.Exists)
                {
                    return null;
                }

                string? macOsDir = libDirInfo.Parent?.FullName;
                if (string.IsNullOrWhiteSpace(macOsDir))
                {
                    return null;
                }

                string pluginsPath = Path.Combine(macOsDir, "plugins");
                return Directory.Exists(pluginsPath) ? pluginsPath : null;
            }
            catch
            {
                return null;
            }
        }

        private static string? TryGetDataPathFromLibPath(string libPath)
        {
            try
            {
                var libDirInfo = new DirectoryInfo(libPath);
                if (!libDirInfo.Exists)
                {
                    return null;
                }

                string? macOsDir = libDirInfo.Parent?.FullName;
                if (string.IsNullOrWhiteSpace(macOsDir))
                {
                    return null;
                }

                string sharePath = Path.Combine(macOsDir, "share");
                return Directory.Exists(sharePath) ? sharePath : null;
            }
            catch
            {
                return null;
            }
        }

        private static void MergePathEnvironment(string variable, string pathToAdd)
        {
            if (string.IsNullOrWhiteSpace(pathToAdd))
            {
                return;
            }

            var existing = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrWhiteSpace(existing))
            {
                Environment.SetEnvironmentVariable(variable, pathToAdd);
                return;
            }

            var separator = Path.PathSeparator;
            var parts = existing
                .Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (!parts.Contains(pathToAdd, StringComparer.Ordinal))
            {
                parts.Insert(0, pathToAdd);
                Environment.SetEnvironmentVariable(variable, string.Join(separator, parts));
            }
        }

        private static IEnumerable<string> GetMacArm64LibVlcCandidatePaths()
        {
            yield return "/Applications/VLC.app/Contents/MacOS/lib";

            foreach (string path in MacArm64LibVlcSearchPaths)
            {
                yield return path;
            }

            // Homebrew cask installs VLC as /opt/homebrew/Caskroom/vlc/<version>/VLC.app/Contents/MacOS/lib
            const string caskRoot = "/opt/homebrew/Caskroom/vlc";
            if (Directory.Exists(caskRoot))
            {
                string[] versionDirs;

                try
                {
                    versionDirs = Directory.GetDirectories(caskRoot);
                }
                catch
                {
                    yield break;
                }

                foreach (string versionDir in versionDirs)
                {
                    string caskLibPath = Path.Combine(versionDir, "VLC.app", "Contents", "MacOS", "lib");
                    yield return caskLibPath;
                }
            }
        }

        public async Task Play(Camera camera, bool displayAdvancedDShowDialog)
        {
            Guard.IsNotNull(camera);

            if (camera.APIType == APIType.Zwo)
            {
                // ZWO cameras bypass LibVLC entirely — frames are rendered directly
                // by FrameRenderer via IZwoFrameSource.
                var zwoFrameSource = Ioc.Default.GetRequiredService<IZwoFrameSource>();

                StopZwoStream();
                zwoFrameSource.Stop();

                bool started = await zwoFrameSource.StartAsync(camera);

                if (!started)
                {
                    logger.Error($"Failed to start ZWO direct stream for camera '{camera.Name}'");
                    SendCameraStateOnUIThread(CameraState.Stopped);
                    return;
                }

                FullAddress = $"zwo-direct://{camera.Path}";
                logger.Info($"ZWO direct stream started for camera '{camera.Name}' ({zwoFrameSource.FrameWidth}×{zwoFrameSource.FrameHeight})");

                SendCameraStateOnUIThread(CameraState.Opening);
                SendCameraStateOnUIThread(CameraState.Playing);
                return;
            }

            if (camera.APIType == APIType.Uvc)
            {
                // UVC cameras on macOS bypass LibVLC entirely — frames are rendered
                // directly by FrameRenderer via IUvcFrameSource (IOKit + libusb).
                logger.Info($"Starting UVC direct stream for camera '{camera.Name}' (VID={camera.VendorId} PID={camera.ProductId})");
                var uvcFrameSource = Ioc.Default.GetRequiredService<IUvcFrameSource>();

                bool started = await uvcFrameSource.StartAsync(camera);

                if (!started)
                {
                    logger.Error($"Failed to start UVC direct stream for camera '{camera.Name}'");
                    SendCameraStateOnUIThread(CameraState.Stopped);
                    return;
                }

                FullAddress = $"uvc-direct://{camera.VendorId}:{camera.ProductId}";
                logger.Info($"UVC direct stream started for camera '{camera.Name}' ({uvcFrameSource.FrameWidth}×{uvcFrameSource.FrameHeight})");

                SendCameraStateOnUIThread(CameraState.Opening);
                SendCameraStateOnUIThread(CameraState.Playing);
                return;
            }

            if (!EnsureInitialized() || libVLC is null || MediaPlayer is null)
            {
                logger.Warn("Ignoring Play request because LibVLC is not available on this platform/runtime.");
                return;
            }

            List<string> parametersList = [];
            ICommandBuilder? commandBuilder = null;

            if (camera.APIType == APIType.LibCamera)
            {
                // Set command type to Vid (video capture)
                commandBuilder = new RpiCameraAppsCommandBuilder
                {
                    CommandType = RpicamAppCommand.Vid
                }.SetDefaultParameters();

                // with libcamera we need first to create video stream
                List<string> controls = new RasPiCameraDetect().GetCommandLineParameters(camera, commandBuilder);
                await AppService.StartRaspberryPIStream(rpiPort, controls);
            }
            else if (camera.APIType == APIType.V4l2)
            {
                parametersList = new V4L2CameraDetect().GetCommandLineParameters(camera, commandBuilder);
            }
            else if (camera.APIType == APIType.Dshow)
            {
                parametersList = new DShowCameraDetect(displayAdvancedDShowDialog).GetCommandLineParameters(camera, commandBuilder);
            }

            if (!string.IsNullOrWhiteSpace(FullAddress))
            {
                string[] mediaAdditionalOptions = [];

                using var media = new Media(
                    libVLC,
                    FullAddress,
                    FromType.FromLocation,
                    mediaAdditionalOptions
                    );

                MediaPlayer.SetAdjustFloat(VideoAdjustOption.Enable, 1);

                foreach (string parameter in parametersList)
                {
                    media.AddOption(parameter);
                }

                bool result = MediaPlayer.Play(media);

                if (result)
                {
                    logger.Info($"Playing web camera stream: '{media.Mrl}'");
                }
                else
                {
                    logger.Info($"Failed to play web camera stream: '{media.Mrl}'");
                }
            }
        }

        private string GetFullUrlFromParts(Camera? camera)
        {
            Guard.IsNotNull(camera);

            protocol = string.Empty;
            pathAndQuery = string.Empty;
            port = string.Empty;
            address = string.Empty;

            if (camera.APIType == APIType.Dshow)
            {
                protocol = "dshow";
            }
            else if (camera.APIType == APIType.QTCapture)
            {
                protocol = "avcapture";
                address = camera.Path;
            }
            else if (camera.APIType == APIType.V4l2)
            {
                protocol = "v4l2";
                address = camera.Path;
            }
            else if (camera.APIType == APIType.LibCamera)
            {
                protocol = "tcp/h264";
                address = "localhost";
                port = rpiPort;
            }
            else if (camera.APIType == APIType.Zwo)
            {
                // Placeholder shown in the UI before the stream starts;
                // the real port is assigned in Play() once the stream is live.
                protocol = "http";
                address = "localhost";
                port = "8091";
            }
            else if (camera.APIType == APIType.Uvc)
            {
                // UVC cameras use direct rendering, not a URL-based stream.
                protocol = "uvc-direct";
                address = $"{camera.VendorId}:{camera.ProductId}";
            }

            string newRemoteAddress = address;
            string addr = newRemoteAddress;
            string pth = string.IsNullOrWhiteSpace(pathAndQuery) ? "" : pathAndQuery;
            string prt = string.IsNullOrWhiteSpace(port) ? "" : $":{port}";

            if (!string.IsNullOrWhiteSpace(protocol))
            {
                protocol += "://";
            }

            return $"{protocol}{addr}{prt}{pth}";
        }

        public string DefaultAddress(Camera camera)
        {
            Guard.IsNotNull(camera);

            FullAddress = GetFullUrlFromParts(camera);
            return FullAddress;
        }        

        public async Task<byte[]?> TakeSnapshotDataAsync()
        {
            Guard.IsNotNull(MediaPlayer);

            if (!MediaPlayer.IsPlaying) return null;

            string tempFile = Path.Combine(Path.GetTempPath(), $"live_collimation_{Guid.NewGuid()}.jpg");
            try
            {
                if (MediaPlayer.TakeSnapshot(0, tempFile, 0, 0))
                {
                    // Wait a moment for file to be written (LibVLC snapshot is async internally)
                    for (int i = 0; i < 5; i++)
                    {
                        if (File.Exists(tempFile))
                        {
                            byte[] data = await File.ReadAllBytesAsync(tempFile);
                            File.Delete(tempFile);
                            return data;
                        }
                        await Task.Delay(50);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to capture live snapshot data");
            }
            return null;
        }

        private void StopZwoStream()
        {
            if (_zwoStream is not null)
            {
                _zwoStream.Stop();
                _zwoStream.Dispose();
                _zwoStream = null;
            }

            try
            {
                Ioc.Default.GetRequiredService<IZwoFrameSource>().Stop();
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "Error stopping ZwoFrameSource");
            }
        }
    }
}
