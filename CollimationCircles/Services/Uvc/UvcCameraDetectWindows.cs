using CollimationCircles.Models;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CollimationCircles.Services.Uvc
{
    /// <summary>
    /// Detects UVC cameras on Windows by enumerating USB devices via SetupAPI
    /// that match the USB Video Class (CC_VIDEO = 0x0E). Returns cameras with
    /// APIType.Uvc, handled by UvcFrameSource (via libuvc) for both streaming
    /// and control — the same code path as macOS and Linux.
    /// </summary>
    internal class UvcCameraDetectWindows() : ICameraDetect
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private const int DIGCF_PRESENT = 0x00000002;
        private const int DIGCF_DEVICEINTERFACE = 0x00000010;
        private const int DIGCF_ALLCLASSES = 0x00000004;
        private const int SPDRP_HARDWAREID = 0x00000001;
        private const int SPDRP_COMPATIBLEIDS = 0x00000002;
        private const int SPDRP_FRIENDLYNAME = 0x0000000C;
        private const int SPDRP_DEVICEDESC = 0x00000000;
        private const int SPDRP_ENUMERATOR_NAME = 0x00000010;
        private const int SPDRP_CLASS = 0x00000007;
        private const int INVALID_HANDLE_VALUE = -1;

        // USB Video Class codes
        private const int USB_CC_VIDEO = 0x0E;
        private const int USB_SUBCLASS_VIDEO_CONTROL = 0x01;
        private const int USB_SUBCLASS_VIDEO_STREAMING = 0x02;

        public Dictionary<ControlType, object> ControlMapping => new()
        {
            // Controls are enumerated at stream start by UvcFrameSource.EnumerateControls
            // (via libuvc), so this mapping is unused.
        };

        public async Task<List<Camera>> GetCameras()
        {
            List<Camera> cameras = [];

            if (!OperatingSystem.IsWindows())
            {
                return cameras;
            }

            try
            {
                await Task.Run(() => DetectUvcCameras(cameras));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error while detecting UVC cameras on Windows");
            }

            return cameras;
        }

        private void DetectUvcCameras(List<Camera> cameras)
        {
            logger.Debug("Starting UVC camera detection...");

            // Enumerate all USB devices
            IntPtr devInfoSet = SetupDiGetClassDevs(
                IntPtr.Zero,
                "USB",
                IntPtr.Zero,
                DIGCF_PRESENT | DIGCF_ALLCLASSES);

            if (devInfoSet == IntPtr.Zero || devInfoSet == new IntPtr(INVALID_HANDLE_VALUE))
            {
                logger.Warn("SetupDiGetClassDevs for USB devices failed");
                return;
            }

            try
            {
                int index = 0;
                int totalDevicesEnumerated = 0;
                var spi = new SP_DEVINFO_DATA();
                spi.cbSize = Marshal.SizeOf(spi);

                for (int memberIndex = 0; SetupDiEnumDeviceInfo(devInfoSet, memberIndex, ref spi); memberIndex++)
                {
                    totalDevicesEnumerated++;
                    try
                    {
                        logger.Debug($"[Device {memberIndex}] Processing USB device...");

                        string? compatibleIds = GetDeviceStringProperty(devInfoSet, ref spi, SPDRP_COMPATIBLEIDS);
                        logger.Debug($"[Device {memberIndex}] Compatible IDs: {(string.IsNullOrEmpty(compatibleIds) ? "<empty>" : compatibleIds)}");

                        if (string.IsNullOrEmpty(compatibleIds))
                        {
                            logger.Debug($"[Device {memberIndex}] Skipped: No compatible IDs");
                            continue;
                        }

                        // Check if this USB device matches the Video Class (UVC)
                        if (!IsUvcDevice(compatibleIds))
                        {
                            logger.Debug($"[Device {memberIndex}] Skipped: Not a UVC device");
                            continue;
                        }

                        logger.Debug($"[Device {memberIndex}] Recognized as UVC device");

                        // Get VID/PID from hardware ID
                        string? hardwareId = GetDeviceStringProperty(devInfoSet, ref spi, SPDRP_HARDWAREID);
                        logger.Debug($"[Device {memberIndex}] Hardware ID: {(string.IsNullOrEmpty(hardwareId) ? "<empty>" : hardwareId)}");

                        if (string.IsNullOrEmpty(hardwareId))
                        {
                            logger.Debug($"[Device {memberIndex}] Skipped: No hardware ID");
                            continue;
                        }

                        int vendorId = 0;
                        int productId = 0;
                        if (!TryExtractVidPid(hardwareId, out vendorId, out productId))
                        {
                            logger.Debug($"[Device {memberIndex}] Skipped: Could not extract VID/PID from '{hardwareId}'");
                            continue;
                        }

                        logger.Debug($"[Device {memberIndex}] Extracted VID: {vendorId:X4}, PID: {productId:X4}");

                        // Get the device name
                        string? deviceName = GetDeviceStringProperty(devInfoSet, ref spi, SPDRP_FRIENDLYNAME);
                        if (string.IsNullOrWhiteSpace(deviceName))
                        {
                            deviceName = GetDeviceStringProperty(devInfoSet, ref spi, SPDRP_DEVICEDESC);
                        }
                        if (string.IsNullOrWhiteSpace(deviceName))
                        {
                            // Fall back to something readable from the hardware ID
                            deviceName = $"UVC Camera ({vendorId:X4}:{productId:X4})";
                        }

                        logger.Debug($"[Device {memberIndex}] Device Name: {deviceName}");

                        // Get the device instance ID for the path
                        string? instanceId = GetDeviceInstanceId(devInfoSet, ref spi);
                        string path = instanceId ?? $"\\\\?\\usb#vid_{vendorId:X4}&pid_{productId:X4}";

                        logger.Debug($"[Device {memberIndex}] Instance ID: {(instanceId ?? "<generated>")}");

                        // Avoid exact duplicates (same VID/PID)
                        if (cameras.Any(c => c.VendorId == vendorId && c.ProductId == productId))
                        {
                            logger.Debug($"[Device {memberIndex}] Skipped: Duplicate VID/PID already in list");
                            continue;
                        }

                        Camera camera = new()
                        {
                            Index = index++,
                            APIType = APIType.Uvc,
                            Name = deviceName,
                            Path = path,
                            VendorId = vendorId,
                            ProductId = productId
                        };

                        camera.Controls = [];
                        cameras.Add(camera);
                        logger.Info($"[Device {memberIndex}] Added Windows UVC camera: '{camera.Name}' (VID={vendorId:X4} PID={productId:X4})");
                    }
                    catch (Exception ex)
                    {
                        logger.Warn(ex, $"Error processing USB device at index {memberIndex}");
                    }
                }

                logger.Info($"USB device enumeration complete: {totalDevicesEnumerated} devices processed, {cameras.Count} UVC camera(s) detected");
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(devInfoSet);
            }
        }

        private static bool IsUvcDevice(string compatibleIds)
        {
            // Check for USB Video Class identifiers in the compatible IDs
            // Format examples from Windows device enumeration:
            //   USB\COMPAT_VID_046d&Class_0e&SubClass_01&Prot_00
            //   USB\COMPAT_VID_046d&Class_0e&SubClass_02&Prot_00
            //   USB\Class_0E (legacy format, less common)
            //   USB\Class_0E&SubClass_01
            string[] ids = compatibleIds.Split('\0', StringSplitOptions.RemoveEmptyEntries);

            logger.Debug($"Checking {ids.Length} compatible ID entries:");
            foreach (var id in ids)
            {
                // Check for:
                // 1. USB\Class_0E (original code's expectation)
                // 2. USB\...&Class_0e&... (Windows device instance format with COMPAT_VID prefix)
                bool matches = id.StartsWith("USB\\Class_0E", StringComparison.OrdinalIgnoreCase) ||
                              id.Contains("&Class_0e&", StringComparison.OrdinalIgnoreCase) ||
                              id.Contains("&Class_0E&", StringComparison.OrdinalIgnoreCase) ||
                              id.Contains("USB_CC_VIDEO", StringComparison.OrdinalIgnoreCase);
                logger.Debug($"  - '{id}' -> {(matches ? "MATCH" : "no match")}");
            }

            bool result = ids.Any(id =>
                id.StartsWith("USB\\Class_0E", StringComparison.OrdinalIgnoreCase) ||
                id.Contains("&Class_0e&", StringComparison.OrdinalIgnoreCase) ||
                id.Contains("&Class_0E&", StringComparison.OrdinalIgnoreCase) ||
                id.Contains("USB_CC_VIDEO", StringComparison.OrdinalIgnoreCase));

            logger.Debug($"IsUvcDevice result: {result}");
            return result;
        }

        /// <summary>
        /// Extracts VID and PID from a USB hardware ID string.
        /// Accepts formats like:
        ///   USB\VID_046D&PID_082D
        ///   USB\VID_046D&PID_082D&REV_0100
        /// </summary>
        private static bool TryExtractVidPid(string hardwareId, out int vendorId, out int productId)
        {
            vendorId = 0;
            productId = 0;

            if (string.IsNullOrWhiteSpace(hardwareId))
                return false;

            // Split on null chars and take the first non-empty line
            string firstLine = hardwareId.Split('\0', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? hardwareId;
            logger.Debug($"Extracting VID/PID from: '{firstLine}'");

            var match = Regex.Match(firstLine,
                @"VID[_=](\w{4})[^0-9A-Fa-f]?PID[_=](\w{4})",
                RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                logger.Debug($"VID/PID regex did not match. Pattern: VID[_=](\\w{{4}})[^0-9A-Fa-f]?PID[_=](\\w{{4}})");
                return false;
            }

            vendorId = int.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            productId = int.Parse(match.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            logger.Debug($"Extracted VID: 0x{vendorId:X4}, PID: 0x{productId:X4}");

            if (!(vendorId > 0 && productId > 0))
            {
                logger.Debug($"VID or PID is invalid (VID={vendorId}, PID={productId})");
                return false;
            }

            return true;
        }

        private static string? GetDeviceStringProperty(IntPtr devInfoSet, ref SP_DEVINFO_DATA devInfoData, int property)
        {
            // Get the required buffer size first
            if (!SetupDiGetDeviceRegistryProperty(devInfoSet, ref devInfoData, property, out int regType, IntPtr.Zero, 0, out int requiredSize))
            {
                int err = Marshal.GetLastWin32Error();
                if (err != 122) // ERROR_INSUFFICIENT_BUFFER
                    return null;
            }

            if (requiredSize <= 0)
                return null;

            IntPtr buffer = Marshal.AllocHGlobal(requiredSize);
            try
            {
                if (!SetupDiGetDeviceRegistryProperty(devInfoSet, ref devInfoData, property, out regType, buffer, requiredSize, out _))
                    return null;

                string result = Marshal.PtrToStringAuto(buffer) ?? string.Empty;
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static string? GetDeviceInstanceId(IntPtr devInfoSet, ref SP_DEVINFO_DATA devInfoData)
        {
            if (!SetupDiGetDeviceInstanceId(devInfoSet, ref devInfoData, IntPtr.Zero, 0, out int requiredSize))
            {
                int err = Marshal.GetLastWin32Error();
                if (err != 122) // ERROR_INSUFFICIENT_BUFFER
                    return null;
            }

            if (requiredSize <= 0)
                return null;

            IntPtr buffer = Marshal.AllocHGlobal(requiredSize * 2);
            try
            {
                if (!SetupDiGetDeviceInstanceId(devInfoSet, ref devInfoData, buffer, requiredSize, out _))
                    return null;

                return Marshal.PtrToStringAuto(buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
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
            if (camera.APIType is APIType.Uvc)
            {
                try
                {
                    logger.Info($"Windows UVC set request: camera='{camera.Name}', control={controlType}, value={value}");
                    var uvcFrameSource = Ioc.Default.GetRequiredService<IUvcFrameSource>();
                    bool ok = uvcFrameSource.SetControl(controlType.ToString(), (long)value);
                    if (!ok)
                    {
                        logger.Warn($"Failed to set UVC control {controlType}={value} on '{camera.Name}'");
                    }
                    else
                    {
                        logger.Info($"Windows UVC set request completed: camera='{camera.Name}', control={controlType}, value={value}");
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
                    logger.Info($"Windows UVC auto set request: camera='{camera.Name}', control={controlType}, isAuto={isAuto}");
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
                            logger.Info($"Windows UVC auto set request completed: camera='{camera.Name}', control={controlType}, isAuto={isAuto}");
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

        // -------------------------------------------------------------------
        // SetupAPI P/Invoke
        // -------------------------------------------------------------------

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SetupDiGetClassDevs(
            IntPtr classGuid,   // null = all classes
            [MarshalAs(UnmanagedType.LPTStr)] string? enumerator,
            IntPtr hwndParent,
            int flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiEnumDeviceInfo(
            IntPtr deviceInfoSet,
            int memberIndex,
            ref SP_DEVINFO_DATA deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiGetDeviceRegistryProperty(
            IntPtr deviceInfoSet,
            ref SP_DEVINFO_DATA deviceInfoData,
            int property,
            out int propertyRegDataType,
            IntPtr propertyBuffer,
            int propertyBufferSize,
            out int requiredSize);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiGetDeviceInstanceId(
            IntPtr deviceInfoSet,
            ref SP_DEVINFO_DATA deviceInfoData,
            IntPtr deviceInstanceId,
            int deviceInstanceIdSize,
            out int requiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public int cbSize;
            public Guid classGuid;
            public int devInst;
            public IntPtr reserved;
        }
    }
}