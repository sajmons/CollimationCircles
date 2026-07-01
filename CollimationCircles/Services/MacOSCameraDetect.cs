using CollimationCircles.Models;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    internal class MacOSCameraDetect : ICameraDetect
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public Dictionary<ControlType, object> ControlMapping => new()
        {
            { ControlType.ExposureTime, "ExposureTime" },
            { ControlType.FocusAbsolute, "FocusAbsolute" },
            { ControlType.WhiteBalance, "WhiteBalance" }
        };

        private sealed class AvFoundationControlDto
        {
            public string Name { get; set; } = string.Empty;
            public int Min { get; set; }
            public int Max { get; set; }
            public double Step { get; set; }
            public int Default { get; set; }
            public int Current { get; set; }
            public bool AutoSupported { get; set; }
            public bool IsAuto { get; set; }
        }

        private sealed class AvFoundationControlsResponse
        {
            public List<AvFoundationControlDto> Controls { get; set; } = [];
        }

        public async Task<List<Camera>> GetCameras()
        {
            List<Camera> cameras = [];

            if (!OperatingSystem.IsMacOS())
            {
                return cameras;
            }

            // First, try to get cameras from system_profiler (built-in cameras)
            await DetectSystemProfilerCameras(cameras);

            return cameras;
        }

        private async Task DetectSystemProfilerCameras(List<Camera> cameras)
        {
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
                        logger.Info($"Added system camera: '{camera.Name}'");
                        addedCount++;
                    }

                    logger.Info($"Parsed {addedCount} cameras from system_profiler");
                }
                else
                {
                    logger.Warn("system_profiler SPCameraDataType -json returned a non-zero exit code");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error while detecting system cameras with system_profiler");
            }
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

                // Use Uvc APIType for cameras with vendor/product IDs (real UVC cameras)
                // Fall back to QTCapture for built-in/virtual cameras without UVC IDs
                APIType apiType = (vendorId > 0 && productId > 0) ? APIType.Uvc : APIType.QTCapture;

                yield return new Camera
                {
                    Index = index++,
                    APIType = apiType,
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
            {
                return false;
            }

            // Accept several common forms returned by macOS tools, for example:
            // VendorID_60324 ProductID_4867
            // Vendor ID: 0xeb94 Product ID: 0x1303
            // VID=60324 PID=4867
            var vidMatch = Regex.Match(
                source,
                @"(?:Vendor\s*ID|VendorID|VID)\s*[_:=-]?\s*(0x[0-9A-Fa-f]+|\d+)",
                RegexOptions.IgnoreCase);
            var pidMatch = Regex.Match(
                source,
                @"(?:Product\s*ID|ProductID|PID)\s*[_:=-]?\s*(0x[0-9A-Fa-f]+|\d+)",
                RegexOptions.IgnoreCase);

            if (!vidMatch.Success || !pidMatch.Success)
            {
                return false;
            }

            if (!TryParseDeviceId(vidMatch.Groups[1].Value, out vendorId))
            {
                return false;
            }

            if (!TryParseDeviceId(pidMatch.Groups[1].Value, out productId))
            {
                return false;
            }

            return vendorId > 0 && productId > 0;
        }

        private static bool TryParseDeviceId(string value, out int id)
        {
            id = 0;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            value = value.Trim();

            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(value[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id);
            }

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

        private async Task DetectUSBCameras(List<Camera> cameras)
        {
            try
            {
                // Try two methods: ioreg and system_profiler
                
                // Method 1: Try ioreg (more detailed but may require more parsing)
                await TryDetectViaIoreg(cameras);
                
                // Method 2: Try system_profiler SPUSBDataType (more reliable)
                await TryDetectViaSystemProfilerUSB(cameras);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error while detecting USB cameras");
            }
        }

        private async Task TryDetectViaIoreg(List<Camera> cameras)
        {
            try
            {
                var (errorCode, result) = await AppService.StartProcessAsync(
                    "ioreg",
                    ["-p", "IOUSB"]);

                if (errorCode == 0)
                {
                    logger.Debug("ioreg output available, parsing for astro cameras");
                    
                    // Look for ASI cameras (ZWO)
                    string asiPattern = @"ASI\s*([0-9]+[A-Z]*)";
                    var matches = Regex.Matches(result, asiPattern, RegexOptions.IgnoreCase);

                    int cameraIndex = 1;
                    foreach (Match match in matches.Cast<Match>())
                    {
                        string name = $"ASI {match.Groups[1].Value}";
                        
                        if (!cameras.Any(c => c.Name == name))
                        {
                            Camera camera = new()
                            {
                                Index = cameras.Count,
                                APIType = APIType.QTCapture,
                                Name = name,
                                Path = cameraIndex.ToString()
                            };

                            camera.Controls = await GetControls(camera);
                            cameras.Add(camera);
                            cameraIndex++;
                            logger.Info($"Added camera via ioreg: '{camera.Name}'");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Debug($"ioreg detection failed (non-critical): {ex.Message}");
            }
        }

        private async Task TryDetectViaSystemProfilerUSB(List<Camera> cameras)
        {
            try
            {
                var (errorCode, result) = await AppService.StartProcessAsync(
                    "system_profiler",
                    ["SPUSBDataType"]);

                if (errorCode == 0)
                {
                    logger.Debug("SPUSBDataType output available, parsing for astro cameras");
                    
                    // Look for common astro camera product names
                    string[] patterns = new[]
                    {
                        @"Product:\s+(.+?ASI.+?)(?=\n|$)",  // ZWO ASI cameras
                        @"Product:\s+(.+?SV.+?)(?=\n|$)",   // SVBONY cameras
                        @"Product:\s+(.+?QHY.+?)(?=\n|$)",  // QHY cameras
                        @"Product:\s+(.+?TouCam.+?)(?=\n|$)" // Philips TouCam
                    };

                    int cameraIndex = 1;
                    foreach (string pattern in patterns)
                    {
                        var matches = Regex.Matches(result, pattern, RegexOptions.IgnoreCase);
                        
                        foreach (Match match in matches.Cast<Match>())
                        {
                            string name = match.Groups[1].Value.Trim();
                            
                            if (!string.IsNullOrWhiteSpace(name) && !cameras.Any(c => c.Name == name))
                            {
                                Camera camera = new()
                                {
                                    Index = cameras.Count,
                                    APIType = APIType.QTCapture,
                                    Name = name,
                                    Path = cameraIndex.ToString()
                                };

                                camera.Controls = await GetControls(camera);
                                cameras.Add(camera);
                                cameraIndex++;
                                logger.Info($"Added USB camera via SPUSBDataType: '{camera.Name}'");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Debug($"SPUSBDataType detection failed (non-critical): {ex.Message}");
            }
        }

        public async Task<List<ICameraControl>> GetControls(Camera camera)
        {
            Guard.IsNotNull(camera);

            List<ICameraControl> controls = [];

            // For UVC cameras with vendor/product IDs, controls are enumerated at
            // stream start by UvcFrameSource.EnumerateControls (via libuvc).
            // Return empty list here — placeholders will be replaced on Play.
            if (camera.APIType is APIType.Uvc && camera.VendorId > 0 && camera.ProductId > 0)
            {
                logger.Info($"UVC camera '{camera.Name}' (VID={camera.VendorId} PID={camera.ProductId}) — controls will be enumerated on stream start");
                return controls;
            }

            // QTCapture cameras: discover controls via AVFoundation (Swift script)
            if (!OperatingSystem.IsMacOS() || camera.APIType is not APIType.QTCapture)
            {
                return controls;
            }

            logger.Info($"Discovering AVFoundation settings for camera '{camera.Name}' ({camera.Path})");

            string script = @"
import Foundation
import AVFoundation

struct Control: Codable {
    let name: String
    let min: Int
    let max: Int
    let step: Double
    let `default`: Int
    let current: Int
    let autoSupported: Bool
    let isAuto: Bool
}

struct Payload: Codable {
    let controls: [Control]
}

func clamp(_ value: Double, _ minValue: Double, _ maxValue: Double) -> Double {
    return max(minValue, min(maxValue, value))
}

let deviceId = CommandLine.arguments.count > 1 ? CommandLine.arguments[1] : """"
let cameraName = CommandLine.arguments.count > 2 ? CommandLine.arguments[2] : """"

let session = AVCaptureDevice.DiscoverySession(deviceTypes: [.builtInWideAngleCamera, .external], mediaType: .video, position: .unspecified)
let devices = session.devices
let device = devices.first { $0.uniqueID == deviceId }
    ?? devices.first { $0.localizedName == cameraName }

var controls: [Control] = []

if let d = device {
    if d.isExposureModeSupported(.continuousAutoExposure) || d.isExposureModeSupported(.locked) {
        controls.append(Control(
            name: ""ExposureTime"",
            min: 0,
            max: 0,
            step: 1,
            default: 0,
            current: 0,
            autoSupported: d.isExposureModeSupported(.continuousAutoExposure),
            isAuto: d.exposureMode == .continuousAutoExposure
        ))
    }

    if d.isFocusModeSupported(.continuousAutoFocus) || d.isFocusModeSupported(.locked) {
        controls.append(Control(
            name: ""FocusAbsolute"",
            min: 0,
            max: 0,
            step: 1,
            default: 0,
            current: 0,
            autoSupported: d.isFocusModeSupported(.continuousAutoFocus),
            isAuto: d.focusMode == .continuousAutoFocus
        ))
    }

    if d.isWhiteBalanceModeSupported(.continuousAutoWhiteBalance) || d.isWhiteBalanceModeSupported(.locked) {
        controls.append(Control(
            name: ""WhiteBalance"",
            min: 0,
            max: 0,
            step: 1,
            default: 0,
            current: 0,
            autoSupported: d.isWhiteBalanceModeSupported(.continuousAutoWhiteBalance),
            isAuto: d.whiteBalanceMode == .continuousAutoWhiteBalance
        ))
    }
}

let payload = Payload(controls: controls)
let data = try JSONEncoder().encode(payload)
print(String(data: data, encoding: .utf8)!)
";

            List<string> swiftArgs = [camera.Path ?? string.Empty, camera.Name ?? string.Empty];
            (int exitCode, string output) = await RunSwiftScriptAsync(script, swiftArgs);

            if (exitCode != 0 || string.IsNullOrWhiteSpace(output))
            {
                logger.Warn($"AVFoundation control discovery failed for '{camera.Name}', exitCode={exitCode}, output={output.Trim()}");
                return controls;
            }

            string? jsonPayload = ExtractFirstJsonObject(output);
            if (string.IsNullOrWhiteSpace(jsonPayload))
            {
                logger.Warn($"AVFoundation control discovery returned no JSON payload for '{camera.Name}'. Raw output: {output.Trim()}");
                return controls;
            }

            AvFoundationControlsResponse? response;
            try
            {
                response = JsonSerializer.Deserialize<AvFoundationControlsResponse>(jsonPayload, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"Failed to parse AVFoundation controls for '{camera.Name}': {output}");
                return controls;
            }

            if (response?.Controls is null || response.Controls.Count == 0)
            {
                logger.Info($"AVFoundation reported no adjustable controls for '{camera.Name}'");
                return controls;
            }

            foreach (AvFoundationControlDto dto in response.Controls)
            {
                if (!Enum.TryParse(dto.Name, out ControlType controlType))
                {
                    logger.Debug($"Skipping unknown AVFoundation control '{dto.Name}' for '{camera.Name}'");
                    continue;
                }

                var control = new CameraControl(controlType, camera);
                control.Min = dto.Min;
                control.Max = dto.Max;
                control.Step = dto.Step;
                control.Default = dto.Default;
                control.AutoSupported = dto.AutoSupported;
                control.Flags = "rw";
                control.ValueType = ControlValueType.Int;
                control.Value = dto.Current;
                control.IsAuto = dto.IsAuto;
                control.IsModeOnly = dto.Min == dto.Max;
                controls.Add(control);
                logger.Info($"AVFoundation control '{dto.Name}' min={dto.Min} max={dto.Max} step={dto.Step} default={dto.Default} value={dto.Current} auto={dto.AutoSupported} modeOnly={control.IsModeOnly} for '{camera.Name}'");
            }

            logger.Info($"Discovered {controls.Count} AVFoundation settings for '{camera.Name}'");

            return controls;
        }

        public List<string> GetCommandLineParameters(Camera camera, ICommandBuilder? builder)
        {
            Guard.IsNotNull(camera);
            return [];
        }

        public void SetControl(Camera camera, ControlType controlType, double value)
        {
            // For UVC cameras, control setting is handled via UvcFrameSource
            // which has the device open during streaming.
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
                return;
            }

            // QTCapture: AVFoundation mode-only controls (numeric set is not supported)
            if (!OperatingSystem.IsMacOS() || camera.APIType is not APIType.QTCapture)
            {
                return;
            }

            logger.Info($"AVFoundation setting update requested: camera='{camera.Name}', control={controlType}, value={value}");

            string mappedName = controlType switch
            {
                ControlType.ExposureTime => "ExposureTime",
                ControlType.FocusAbsolute => "FocusAbsolute",
                ControlType.WhiteBalance => "WhiteBalance",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(mappedName))
            {
                logger.Debug($"Ignoring unsupported AVFoundation control set for {controlType} on '{camera.Name}'");
                return;
            }

            logger.Info($"Ignored numeric AVFoundation setting update (mode-only control): camera='{camera.Name}', control={controlType}, value={value}, mappedName={mappedName}");
        }

        public void SetControlAuto(Camera camera, ControlType controlType, bool isAuto)
        {
            // UVC cameras: route to UvcFrameSource (libuvc)
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
                return;
            }

            // QTCapture: set auto mode via AVFoundation Swift script
            if (!OperatingSystem.IsMacOS() || camera.APIType is not APIType.QTCapture)
            {
                return;
            }

            logger.Info($"AVFoundation auto-setting update requested: camera='{camera.Name}', control={controlType}, isAuto={isAuto}");

            string mappedName = controlType switch
            {
                ControlType.ExposureTime => "ExposureTime",
                ControlType.FocusAbsolute => "FocusAbsolute",
                ControlType.WhiteBalance => "WhiteBalance",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(mappedName))
            {
                logger.Debug($"Ignoring unsupported AVFoundation auto-control set for {controlType} on '{camera.Name}'");
                return;
            }

            string script = @"
import Foundation
import AVFoundation

let deviceId = CommandLine.arguments.count > 1 ? CommandLine.arguments[1] : """"
let cameraName = CommandLine.arguments.count > 2 ? CommandLine.arguments[2] : """"
let control = CommandLine.arguments.count > 3 ? CommandLine.arguments[3] : """"
let autoEnabled = CommandLine.arguments.count > 4 ? (CommandLine.arguments[4] == ""1"") : false

let session = AVCaptureDevice.DiscoverySession(deviceTypes: [.builtInWideAngleCamera, .external], mediaType: .video, position: .unspecified)
let devices = session.devices
guard let device = devices.first(where: { $0.uniqueID == deviceId })
    ?? devices.first(where: { $0.localizedName == cameraName }) else {
    print(""device-not-found"")
    Foundation.exit(2)
}

do {
    try device.lockForConfiguration()
    defer { device.unlockForConfiguration() }

    switch control {
    case ""ExposureTime"":
        if autoEnabled {
            guard device.isExposureModeSupported(.continuousAutoExposure) else {
                print(""unsupported"")
                Foundation.exit(3)
            }
            device.exposureMode = .continuousAutoExposure
        } else {
            guard device.isExposureModeSupported(.locked) else {
                print(""unsupported"")
                Foundation.exit(3)
            }
            device.exposureMode = .locked
        }

    case ""FocusAbsolute"":
        if autoEnabled {
            guard device.isFocusModeSupported(.continuousAutoFocus) else {
                print(""unsupported"")
                Foundation.exit(3)
            }
            device.focusMode = .continuousAutoFocus
        } else {
            guard device.isFocusModeSupported(.locked) else {
                print(""unsupported"")
                Foundation.exit(3)
            }
            device.focusMode = .locked
        }

    case ""WhiteBalance"":
        if autoEnabled {
            guard device.isWhiteBalanceModeSupported(.continuousAutoWhiteBalance) else {
                print(""unsupported"")
                Foundation.exit(3)
            }
            device.whiteBalanceMode = .continuousAutoWhiteBalance
        } else {
            guard device.isWhiteBalanceModeSupported(.locked) else {
                print(""unsupported"")
                Foundation.exit(3)
            }
            device.whiteBalanceMode = .locked
        }

    default:
        print(""unsupported"")
        Foundation.exit(3)
    }

    print(""ok"")
} catch {
    print(""lock-failed: \(error)"")
    Foundation.exit(4)
}
";

            try
            {
                List<string> swiftArgs =
                [
                    camera.Path ?? string.Empty,
                    camera.Name ?? string.Empty,
                    mappedName,
                    isAuto ? "1" : "0"
                ];

                (int exitCode, string output) = RunSwiftScriptAsync(script, swiftArgs).ConfigureAwait(false).GetAwaiter().GetResult();
                if (exitCode == 0)
                {
                    logger.Info($"macOS AVFoundation auto-control set completed: camera='{camera.Name}', control={controlType}, isAuto={isAuto}");
                }
                else
                {
                    logger.Warn($"Failed AVFoundation auto-control set for '{camera.Name}', control={controlType}, isAuto={isAuto}, exitCode={exitCode}, output={output.Trim()}");
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Error setting AVFoundation auto control {controlType} on '{camera.Name}'");
            }
        }

        private static string? ExtractFirstJsonObject(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
            {
                return null;
            }

            int start = output.IndexOf('{');
            if (start < 0)
            {
                return null;
            }

            int depth = 0;
            bool inString = false;
            bool escaped = false;

            for (int i = start; i < output.Length; i++)
            {
                char c = output[i];

                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                {
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return output[start..(i + 1)];
                    }
                }
            }

            return null;
        }

        private static async Task<(int ExitCode, string Output)> RunSwiftScriptAsync(string script, List<string> args)
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"cc-avf-{Guid.NewGuid():N}.swift");

            try
            {
                await File.WriteAllTextAsync(tempFile, script).ConfigureAwait(false);
                List<string> swiftArgs = [tempFile];
                swiftArgs.AddRange(args);
                return await AppService.StartProcessAsync("/usr/bin/swift", swiftArgs).ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch
                {
                    // Best effort cleanup only.
                }
            }
        }
    }
}
