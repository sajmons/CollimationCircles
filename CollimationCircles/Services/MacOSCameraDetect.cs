using CollimationCircles.Models;
using CommunityToolkit.Diagnostics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    internal class MacOSCameraDetect : ICameraDetect
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

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

        public Dictionary<ControlType, object> ControlMapping => new()
        {
            { ControlType.ExposureTime, "ExposureTime" },
            { ControlType.FocusAbsolute, "FocusAbsolute" },
            { ControlType.WhiteBalance, "WhiteBalance" }
        };

        public async Task<List<Camera>> GetCameras()
        {
            List<Camera> cameras = [];

            if (!OperatingSystem.IsMacOS())
            {
                return cameras;
            }

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

                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(uniqueId))
                {
                    continue;
                }

                yield return new Camera
                {
                    Index = index++,
                    APIType = APIType.QTCapture,
                    Name = name,
                    Path = uniqueId.Trim()
                };
            }
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

            List<ICameraControl> controls = [];

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

let devices = AVCaptureDevice.devices(for: .video)
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

let devices = AVCaptureDevice.devices(for: .video)
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
