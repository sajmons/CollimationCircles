using CollimationCircles.Models;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CollimationCircles.Services.Uvc
{
    /// <summary>
    /// Detects UVC cameras on Linux by scanning /sys/class/video4linux/ for USB
    /// video devices and extracting vendor/product IDs. The returned cameras use
    /// APIType.Uvc and are handled by UvcFrameSource (via libuvc) for both
    /// streaming and control — the same code path as macOS.
    ///
    /// This replaces the old V4L2CameraDetect which used v4l2-ctl subprocess
    /// calls and required separate V4L2-specific streaming/control logic.
    /// </summary>
    internal class UvcCameraDetectLinux() : ICameraDetect
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public Dictionary<ControlType, object> ControlMapping => new()
        {
            // Controls are enumerated at stream start by UvcFrameSource.EnumerateControls
            // (via libuvc), so this mapping is unused.
        };

        public async Task<List<Camera>> GetCameras()
        {
            List<Camera> cameras = [];

            if (!OperatingSystem.IsLinux())
            {
                logger.Debug("UvcCameraDetectLinux.GetCameras: skipped — not Linux");
                return cameras;
            }

            logger.Info("UvcCameraDetectLinux.GetCameras: begin");

            try
            {
                await Task.Run(() => DetectUvcCameras(cameras));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error while detecting UVC cameras on Linux");
            }

            logger.Info($"UvcCameraDetectLinux.GetCameras: returning {cameras.Count} camera(s)");
            return cameras;
        }

        private void DetectUvcCameras(List<Camera> cameras)
        {
            string videoDevicesDir = "/sys/class/video4linux";
            if (!Directory.Exists(videoDevicesDir))
            {
                logger.Warn($"UvcCameraDetectLinux: /sys/class/video4linux not found — no video devices detected");
                return;
            }

            string[] videoDeviceDirs = Directory.GetDirectories(videoDevicesDir);
            logger.Info($"UvcCameraDetectLinux: found {videoDeviceDirs.Length} video device(s) in {videoDevicesDir}");

            int index = 0;

            foreach (string deviceDir in videoDeviceDirs)
            {
                try
                {
                    logger.Debug($"UvcCameraDetectLinux: processing '{deviceDir}'");

                    // Resolve the device symlink to a real path
                    string realPath = ResolveSymlink(deviceDir);
                    if (string.IsNullOrEmpty(realPath))
                    {
                        logger.Debug($"UvcCameraDetectLinux: could not resolve symlink for '{deviceDir}'");
                        continue;
                    }

                    logger.Debug($"UvcCameraDetectLinux: real path = '{realPath}'");

                    // The device path contains the /dev/video* name
                    string deviceName = Path.GetFileName(deviceDir);
                    string devPath = $"/dev/{deviceName}";

                    // Read the device name from the 'name' file
                    string? name = ReadFileContent(Path.Combine(deviceDir, "name"))?.Trim();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        name = deviceName;
                        logger.Debug($"UvcCameraDetectLinux: no name file, using '{deviceName}'");
                    }

                    // Check if this is a USB device by looking for idVendor/idProduct
                    // in the device's parent USB hierarchy
                    int vendorId = 0;
                    int productId = 0;
                    string? usbDevicePath = FindUsbDevicePath(realPath);

                    if (usbDevicePath != null)
                    {
                        logger.Debug($"UvcCameraDetectLinux: found USB device path '{usbDevicePath}'");
                        string? vidStr = ReadFileContent(Path.Combine(usbDevicePath, "idVendor"));
                        string? pidStr = ReadFileContent(Path.Combine(usbDevicePath, "idProduct"));

                        if (!string.IsNullOrWhiteSpace(vidStr) && !string.IsNullOrWhiteSpace(pidStr))
                        {
                            vendorId = ParseHexId(vidStr.Trim());
                            productId = ParseHexId(pidStr.Trim());
                            logger.Debug($"UvcCameraDetectLinux: parsed VID={vendorId} PID={productId} from '{vidStr.Trim()}' / '{pidStr.Trim()}'");
                        }
                        else
                        {
                            logger.Debug($"UvcCameraDetectLinux: idVendor or idProduct not found at '{usbDevicePath}'");
                        }
                    }
                    else
                    {
                        logger.Debug($"UvcCameraDetectLinux: no USB device path found for real path '{realPath}'");
                    }

                    // Only add cameras with valid USB VID/PID (UVC cameras)
                    if (vendorId > 0 && productId > 0)
                    {
                        Camera camera = new()
                        {
                            Index = index++,
                            APIType = APIType.Uvc,
                            Name = name,
                            Path = devPath,
                            VendorId = vendorId,
                            ProductId = productId
                        };

                        // Controls are enumerated at stream start by UvcFrameSource
                        camera.Controls = [];

                        cameras.Add(camera);
                        logger.Info($"UvcCameraDetectLinux: added UVC camera '{camera.Name}' (VID={vendorId} PID={productId}) at {devPath}");
                    }
                    else
                    {
                        logger.Debug($"UvcCameraDetectLinux: skipping non-USB video device '{name}' at {devPath} (VID={vendorId} PID={productId})");
                    }
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, $"UvcCameraDetectLinux: error processing video device '{deviceDir}'");
                }
            }

            logger.Info($"UvcCameraDetectLinux: detected {cameras.Count} UVC camera(s)");
        }

        /// <summary>
        /// Walks up the device tree from the video device's real path to find
        /// the USB device node containing idVendor/idProduct files.
        /// </summary>
        private static string? FindUsbDevicePath(string deviceRealPath)
        {
            // Walk up the directory tree looking for a USB device with idVendor/idProduct
            DirectoryInfo? dir = new DirectoryInfo(deviceRealPath);

            for (int i = 0; i < 20 && dir != null; i++)
            {
                string vidPath = Path.Combine(dir.FullName, "idVendor");
                string pidPath = Path.Combine(dir.FullName, "idProduct");

                if (File.Exists(vidPath) && File.Exists(pidPath))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }

            return null;
        }

        /// <summary>
        /// Resolves a symlink to its real (absolute) path. Returns null on failure.
        /// </summary>
        private static string? ResolveSymlink(string path)
        {
            try
            {
                var linkTarget = File.ResolveLinkTarget(path, true);
                return linkTarget?.FullName;
            }
            catch
            {
                return null;
            }
        }

        private static string? ReadFileContent(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    return File.ReadAllText(path).Trim();
                }
            }
            catch (Exception ex)
            {
                logger.Debug(ex, $"Could not read '{path}'");
            }
            return null;
        }

        private static int ParseHexId(string value)
        {
            value = value.Trim();
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                value = value[2..];
            }
            return int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int id) ? id : 0;
        }

        public async Task<List<ICameraControl>> GetControls(Camera camera)
        {
            Guard.IsNotNull(camera);

            // UVC controls are enumerated at stream start by UvcFrameSource.EnumerateControls
            // (via libuvc). Return empty list — placeholders will be replaced on Play.
            logger.Info($"UVC camera '{camera.Name}' (VID={camera.VendorId} PID={camera.ProductId}) — controls will be enumerated on stream start");
            return await Task.FromResult(new List<ICameraControl>());
        }

        public void SetControl(Camera camera, ControlType controlType, double value)
        {
            // UVC control setting is handled via UvcFrameSource
            // which has the device open during streaming.
            if (camera.APIType is APIType.Uvc)
            {
                try
                {
                    logger.Info($"Linux UVC set request: camera='{camera.Name}', control={controlType}, value={value}");
                    var uvcFrameSource = Ioc.Default.GetRequiredService<IUvcFrameSource>();
                    bool ok = uvcFrameSource.SetControl(controlType.ToString(), (long)value);
                    if (!ok)
                    {
                        logger.Warn($"Failed to set UVC control {controlType}={value} on '{camera.Name}'");
                    }
                    else
                    {
                        logger.Info($"Linux UVC set request completed: camera='{camera.Name}', control={controlType}, value={value}");
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Error setting UVC control {controlType} on '{camera.Name}'");
                }
            }
        }

        public void SetControlAuto(Camera camera, ControlType controlType, bool isAuto)
        {
            if (camera.APIType is APIType.Uvc)
            {
                try
                {
                    logger.Info($"Linux UVC auto set request: camera='{camera.Name}', control={controlType}, isAuto={isAuto}");
                    var uvcFrameSource = Ioc.Default.GetRequiredService<IUvcFrameSource>();

                    string autoName = controlType switch
                    {
                        ControlType.ExposureTime => "AutoExposure",
                        ControlType.FocusAbsolute => "AutoFocus",
                        ControlType.WhiteBalance => "AutoWhiteBalance",
                        ControlType.Hue => "HueAuto",
                        ControlType.Contrast => "ContrastAuto",
                        _ => string.Empty
                    };

                    if (!string.IsNullOrEmpty(autoName))
                    {
                        bool ok = uvcFrameSource.SetAutoControl(autoName, isAuto);
                        if (!ok)
                        {
                            logger.Warn($"Failed to set UVC auto control {controlType}={isAuto} on '{camera.Name}'");
                        }
                        else
                        {
                            logger.Info($"Linux UVC auto set request completed: camera='{camera.Name}', control={controlType}, isAuto={isAuto}");
                        }
                    }
                    else
                    {
                        logger.Warn($"No UVC auto-control mapping for {controlType} on '{camera.Name}'");
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, $"Error setting UVC auto control {controlType} on '{camera.Name}'");
                }
            }
        }

        public List<string> GetCommandLineParameters(Camera camera, ICommandBuilder? builder)
        {
            Guard.IsNotNull(camera);
            return [];
        }
    }
}