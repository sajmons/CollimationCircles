using CollimationCircles.Models;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CollimationCircles.Services.Uvc
{
    /// <summary>
    /// Detects UVC cameras on macOS by parsing system_profiler SPCameraDataType
    /// output and extracting vendor/product IDs from the model-id string.
    /// Returns cameras with APIType.Uvc, handled by UvcFrameSource (via libuvc)
    /// for both streaming and control — the same code path as Linux and Windows.
    /// </summary>
    internal class UvcCameraDetectMac : ICameraDetect
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

            if (!OperatingSystem.IsMacOS())
            {
                return cameras;
            }

            try
            {
                var (errorCode, result) = await AppService.StartProcessAsync(
                    "system_profiler",
                    ["SPCameraDataType", "-json"]);

                logger.Info($"system_profiler SPCameraDataType -json exit code: {errorCode}");

                if (errorCode == 0)
                {
                    int addedCount = 0;

                    foreach (Camera camera in ParseSystemProfilerCameras(result, cameras.Count))
                    {
                        camera.Controls = await GetControls(camera);
                        cameras.Add(camera);
                        logger.Info($"Added UVC camera: '{camera.Name}'");
                        addedCount++;
                    }

                    logger.Info($"Parsed {addedCount} UVC cameras from system_profiler");
                }
                else
                {
                    logger.Warn("system_profiler SPCameraDataType -json returned a non-zero exit code");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error while detecting UVC cameras on macOS");
            }

            return cameras;
        }

        private static IEnumerable<Camera> ParseSystemProfilerCameras(string json, int startIndex)
        {
            using JsonDocument document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty("SPCameraDataType", out JsonElement camerasElement) ||
                camerasElement.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }

            int index = startIndex;

            foreach (JsonElement cameraElement in camerasElement.EnumerateArray())
            {
                string? name = TryGetString(cameraElement, "_name");
                string? uniqueId = TryGetString(cameraElement, "spcamera_unique-id");
                string? modelId = TryGetString(cameraElement, "spcamera_model-id");

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(uniqueId))
                    continue;

                // Parse vendor/product IDs from model-id string like:
                // "UVC Camera VendorID_60324 ProductID_4867"
                int vendorId = 0;
                int productId = 0;

                if (!string.IsNullOrWhiteSpace(modelId))
                {
                    _ = TryExtractVidPid(modelId, out vendorId, out productId);
                }

                // Only yield cameras with valid vendor/product IDs (real UVC cameras)
                if (vendorId <= 0 || productId <= 0)
                    continue;

                yield return new Camera
                {
                    Index = index++,
                    APIType = APIType.Uvc,
                    Name = name,
                    Path = uniqueId.Trim(),
                    VendorId = vendorId,
                    ProductId = productId
                };
            }
        }

        private static bool TryExtractVidPid(string source, out int vendorId, out int productId)
        {
            vendorId = 0;
            productId = 0;

            if (string.IsNullOrWhiteSpace(source))
                return false;

            var vidMatch = Regex.Match(
                source,
                @"(?:Vendor\s*ID|VendorID|VID)\s*[_:=-]?\s*(0x[0-9A-Fa-f]+|\d+)",
                RegexOptions.IgnoreCase);
            var pidMatch = Regex.Match(
                source,
                @"(?:Product\s*ID|ProductID|PID)\s*[_:=-]?\s*(0x[0-9A-Fa-f]+|\d+)",
                RegexOptions.IgnoreCase);

            if (!vidMatch.Success || !pidMatch.Success)
                return false;

            if (!TryParseDeviceId(vidMatch.Groups[1].Value, out vendorId))
                return false;

            if (!TryParseDeviceId(pidMatch.Groups[1].Value, out productId))
                return false;

            return vendorId > 0 && productId > 0;
        }

        private static bool TryParseDeviceId(string value, out int id)
        {
            id = 0;

            if (string.IsNullOrWhiteSpace(value))
                return false;

            value = value.Trim();

            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return int.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id);

            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
        }

        private static string? TryGetString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement propertyElement) ||
                propertyElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            return propertyElement.GetString();
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
                    logger.Info($"macOS UVC set request: camera='{camera.Name}', control={controlType}, value={value}");
                    var uvcFrameSource = Ioc.Default.GetRequiredService<IUvcFrameSource>();
                    bool ok = uvcFrameSource.SetControl(controlType.ToString(), (long)value);
                    if (!ok)
                    {
                        logger.Warn($"Failed to set UVC control {controlType}={value} on '{camera.Name}'");
                    }
                    else
                    {
                        logger.Info($"macOS UVC set request completed: camera='{camera.Name}', control={controlType}, value={value}");
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
                    logger.Info($"macOS UVC auto set request: camera='{camera.Name}', control={controlType}, isAuto={isAuto}");
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
                            logger.Info($"macOS UVC auto set request completed: camera='{camera.Name}', control={controlType}, isAuto={isAuto}");
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