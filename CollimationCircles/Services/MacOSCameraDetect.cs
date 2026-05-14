using CollimationCircles.Models;
using CommunityToolkit.Diagnostics;
using System;
using System.Collections.Generic;
using System.Globalization;
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
            { ControlType.Gain, "gain" },
            { ControlType.ExposureTime, "exposure" }
        };

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

        public Task<List<ICameraControl>> GetControls(Camera camera)
        {
            Guard.IsNotNull(camera);

            List<ICameraControl> controls = [];

            // Add Gain control for astro cameras (typical range: 0-600 for ZWO)
            var gainControl = new CameraControl(ControlType.Gain, camera)
            {
                Min = 0,
                Max = 600,
                Step = 1,
                Default = 100,
                Value = 100,
                Flags = "ro",
                ValueType = ControlValueType.Int
            };
            controls.Add(gainControl);
            logger.Info($"Gain control added for '{camera.Name}'");

            // Add Exposure Time control (typical range: 1-10000 ms for astro cameras)
            var exposureControl = new CameraControl(ControlType.ExposureTime, camera)
            {
                Min = 1,
                Max = 10000,
                Step = 1,
                Default = 100,
                Value = 100,
                Flags = "ro",
                ValueType = ControlValueType.Int
            };
            controls.Add(exposureControl);
            logger.Info($"Exposure Time control added for '{camera.Name}'");

            return Task.FromResult(controls);
        }

        public List<string> GetCommandLineParameters(Camera camera, ICommandBuilder? builder)
        {
            Guard.IsNotNull(camera);
            return [];
        }

        public void SetControl(Camera camera, ControlType controlType, double value)
        {
            // Note: Camera controls via standard macOS CLI are not supported.
            // On macOS, UVC camera control would require integration with:
            // - AVFoundation (Apple's native framework)
            // - libuvc (third-party library)
            // - IOKit (low-level device control)
            // For now, this is a UI placeholder. Actual control requires native integration.
            
            logger.Warn($"SetControl called for {controlType}={value} on macOS camera '{camera.Name}', but CLI control is not implemented.");
        }
    }
}
