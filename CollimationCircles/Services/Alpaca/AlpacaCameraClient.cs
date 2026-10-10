using ASCOM.Alpaca.Clients;
using ASCOM.Alpaca.Discovery;
using ASCOM.Common.Alpaca;
using CollimationCircles.Messages;
using CollimationCircles.Models;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace CollimationCircles.Services.Alpaca
{
    internal class AlpacaCameraClient : ICameraDetect
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private const double DefaultExposureSec = 1.0;
        private const int DefaultBinning = 2;
        private const int MaxDirectProbeDeviceNumber = 7;
        private const int DiscoveryTimeoutSeconds = 5;
        private const short MaxFallbackProbeBin = 4;

        private static readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(DiscoveryTimeoutSeconds) };

        private readonly object sync = new();

        private AlpacaCamera? device;
        private CancellationTokenSource? cts;
        private Task? loop;

        private int requestedBin = DefaultBinning;
        private double requestedGain;
        private double requestedExposureSec = DefaultExposureSec;
        private bool gainSupported;

        private bool connected;

        public bool IsConnected => connected;

        public async Task<List<Camera>> Connect(string address, int port)
        {
            await Disconnect();

            logger.Info($"Connecting to Alpaca server at {address}:{port}");

            try
            {
                var cameras = await DiscoverAsync(address, port);
                connected = true;
                logger.Info($"Connected to Alpaca server at {address}:{port}, found {cameras.Count} camera(s)");
                return cameras;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"Alpaca connection failed for {address}:{port}");
                throw;
            }
        }

        public async Task Disconnect()
        {
            var wasConnected = connected;
            connected = false;

            if (!wasConnected && device is null)
            {
                return;
            }

            logger.Info("Disconnecting from Alpaca server");
            await Stop(new Camera());
        }

        private static async Task<List<Camera>> DiscoverAsync(string address, int port)
        {
            var cameras = await GetConfiguredCamerasAsync(address, port);

            if (cameras.Count == 0)
            {
                logger.Warn($"Alpaca HTTP configureddevices discovery found no cameras for {address}:{port}. Trying direct camera endpoint probing.");
                cameras = await ProbeCamerasByDeviceNumberAsync(address, port, MaxDirectProbeDeviceNumber);
            }

            return cameras;
        }

        private static async Task<List<Camera>> GetConfiguredCamerasAsync(string address, int port)
        {
            var endpoint = BuildConfiguredDevicesEndpoint(address, port);

            try
            {
                using var response = await httpClient.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var json = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

                if (TryReadErrorNumber(json.RootElement, out var errorNumber) && errorNumber != 0)
                {
                    var message = TryReadString(json.RootElement, "ErrorMessage") ?? "Unknown error";
                    logger.Warn($"Alpaca configureddevices request failed for {address}:{port}. ErrorNumber={errorNumber}, ErrorMessage={message}");
                    return new List<Camera>();
                }

                if (!TryGetDeviceArray(json.RootElement, out var devicesElement))
                {
                    return new List<Camera>();
                }

                var cameras = new List<Camera>();
                foreach (var item in devicesElement.EnumerateArray())
                {
                    var deviceType = TryReadString(item, "DeviceType");
                    if (!string.Equals(deviceType, "Camera", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!TryReadInt(item, "DeviceNumber", out var deviceNumber))
                    {
                        continue;
                    }

                    var name = TryReadString(item, "DeviceName");
                    var uniqueId = TryReadString(item, "UniqueID") ?? TryReadString(item, "UniqueId") ?? string.Empty;

                    cameras.Add(new Camera
                    {
                        Index = cameras.Count,
                        Name = string.IsNullOrWhiteSpace(name) ? $"Camera {deviceNumber}" : name,
                        APIType = APIType.Alpaca,
                        ServerAddress = address,
                        ServerPort = port,
                        DeviceNumber = deviceNumber,
                        AlpacaUniqueId = uniqueId
                    });
                }

                logger.Info($"Alpaca HTTP configureddevices discovery found {cameras.Count} camera(s) at {address}:{port}");
                return cameras.OrderBy(c => c.Name ?? string.Empty).ToList();
            }
            catch (Exception ex)
            {
                logger.Debug(ex, $"Alpaca HTTP configureddevices discovery failed for {address}:{port}");
                return new List<Camera>();
            }
        }

        private static Uri BuildConfiguredDevicesEndpoint(string address, int port)
        {
            var host = address.Trim().Trim('[', ']');
            if (IPAddress.TryParse(host, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                host = $"[{host}]";
            }

            return new Uri($"http://{host}:{port}/management/v1/configureddevices");
        }

        private static bool TryGetDeviceArray(JsonElement root, out JsonElement devices)
        {
            if (root.ValueKind == JsonValueKind.Array)
            {
                devices = root;
                return true;
            }

            if (TryGetProperty(root, "Value", out var value) && value.ValueKind == JsonValueKind.Array)
            {
                devices = value;
                return true;
            }

            devices = default;
            return false;
        }

        private static bool TryReadErrorNumber(JsonElement element, out int value)
        {
            return TryReadInt(element, "ErrorNumber", out value);
        }

        private static bool TryReadInt(JsonElement element, string propertyName, out int value)
        {
            value = default;
            if (!TryGetProperty(element, propertyName, out var property))
            {
                return false;
            }

            return property.ValueKind switch
            {
                JsonValueKind.Number => property.TryGetInt32(out value),
                JsonValueKind.String => int.TryParse(property.GetString(), out value),
                _ => false
            };
        }

        private static string? TryReadString(JsonElement element, string propertyName)
        {
            if (!TryGetProperty(element, propertyName, out var property))
            {
                return null;
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }

            return property.ValueKind switch
            {
                JsonValueKind.Number => property.GetRawText(),
                JsonValueKind.True => bool.TrueString,
                JsonValueKind.False => bool.FalseString,
                _ => null
            };
        }

        private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static async Task<List<Camera>> ProbeCamerasByDeviceNumberAsync(string address, int port, int maxDeviceNumber)
        {
            var cameras = new List<Camera>();

            for (int deviceNumber = 0; deviceNumber <= maxDeviceNumber; deviceNumber++)
            {
                var dev = new AlpacaCamera(ServiceType.Http, address, port, deviceNumber, true, null);
                try
                {
                    var cameraName = await Task.Run(() =>
                    {
                        dev.Connected = true;
                        return dev.Name;
                    });

                    cameras.Add(new Camera
                    {
                        Index = cameras.Count,
                        Name = string.IsNullOrWhiteSpace(cameraName) ? $"Camera {deviceNumber}" : cameraName,
                        APIType = APIType.Alpaca,
                        ServerAddress = address,
                        ServerPort = port,
                        DeviceNumber = deviceNumber
                    });
                }
                catch (ASCOM.DriverException ex) when (IsExpectedProbeMiss(ex))
                {
                    logger.Trace($"Alpaca camera probe skipped for {address}:{port} device {deviceNumber}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    logger.Debug($"Alpaca camera probe failed for {address}:{port} device {deviceNumber}: {ex.Message}");
                }
                finally
                {
                    try { dev.Connected = false; } catch { }
                    DisposeQuietly(dev);
                }
            }

            logger.Info($"Direct camera probing found {cameras.Count} camera(s) at {address}:{port}");
            return cameras;
        }

        private static bool IsExpectedProbeMiss(ASCOM.DriverException ex)
        {
            return ex.Message.Contains("\"ErrorNumber\":1003", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("\"ErrorNumber\":1004", StringComparison.OrdinalIgnoreCase);
        }

        public async Task Start(Camera camera)
        {
            await Stop(camera);

            if (!connected)
            {
                throw new InvalidOperationException("Not connected to an Alpaca server.");
            }

            // Alpaca servers
            var current = await DiscoverAsync(camera.ServerAddress, camera.ServerPort);
            var match = current.FirstOrDefault(c => c.Name == camera.Name);
            if (match is not null && match.DeviceNumber != camera.DeviceNumber)
            {
                logger.Info($"Alpaca device number for '{camera.Name}' changed from {camera.DeviceNumber} to {match.DeviceNumber}");
                camera.DeviceNumber = match.DeviceNumber;
            }

            logger.Info($"Connecting to Alpaca camera at {camera.ServerAddress}:{camera.ServerPort}, device number {camera.DeviceNumber}");
            var dev = new AlpacaCamera(ServiceType.Http, camera.ServerAddress, camera.ServerPort, camera.DeviceNumber, true, null);
            dev.ImageArrayTransferType = ImageArrayTransferType.BestAvailable;
            
            try
            {
                camera.Controls = await Task.Run(() =>
                {
                    dev.Connected = true;
                    return BuildControls(camera, dev);
                });
            }
            catch (TimeoutException ex)
            {
                logger.Error(ex, $"Timed out connecting to Alpaca camera at {camera.ServerAddress}:{camera.ServerPort}. Check that the server is running and reachable.");
                DisposeQuietly(dev);
                throw;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Failed to connect to Alpaca camera at {camera.ServerAddress}:{camera.ServerPort}, device number {camera.DeviceNumber}");
                DisposeQuietly(dev);
                throw;
            }

            logger.Info($"Connected to Alpaca camera '{camera.Name}'");
            device = dev;
            cts = new CancellationTokenSource();
            var token = cts.Token;
            loop = Task.Run(() => CaptureLoop(dev, token));
        }

        private static void DisposeQuietly(AlpacaCamera dev)
        {
            try
            {
                dev.Dispose();
            }
            catch (Exception ex)
            {
                logger.Warn($"Alpaca device dispose failed: {ex.Message}");
            }
        }

        public async Task Stop(Camera camera)
        {
            var source = cts;
            var task = loop;
            var dev = device;
            cts = null;
            loop = null;
            device = null;

            if (source is null && dev is null)
            {
                return;
            }

            source?.Cancel();

            try
            {
                if (task is not null)
                {
                    await task;
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                logger.Warn($"Alpaca capture loop ended with error: {ex.Message}");
            }

            if (dev is not null)
            {
                await Task.Run(() =>
                {
                    try { dev.Connected = false; } catch (Exception ex) { logger.Warn($"Alpaca disconnect failed: {ex.Message}"); }
                    DisposeQuietly(dev);
                });
            }

            source?.Dispose();
        }

        public void SetControl(Camera camera, ControlType controlName, double value)
        {
            lock (sync)
            {
                switch (controlName)
                {
                    case ControlType.Binning:
                        requestedBin = Math.Max(1, (int)Math.Round(value));
                        break;
                    case ControlType.Gain:
                        requestedGain = value;
                        break;
                    case ControlType.ExposureTime:
                        requestedExposureSec = Math.Max(0.001, value);
                        break;
                }
            }
        }

        private List<ICameraControl> BuildControls(Camera camera, AlpacaCamera dev)
        {
            var controls = new List<ICameraControl>();

            var maxBin = GetMaxSupportedBin(dev);
            var currentBin = Math.Clamp(DefaultBinning, 1, maxBin);
            requestedBin = currentBin;
            var bin = new CameraControl(ControlType.Binning, camera);
            bin.ApplyDiscoveredState(1, maxBin, 1, currentBin, currentBin, false, false, string.Empty, ControlValueType.Int);
            controls.Add(bin);

            gainSupported = false;
            try
            {
                int min = 0;
                int max = 100;
                int cur = 0;
                bool gainInfoFound = false;

                try { min = dev.GainMin; gainInfoFound = true; } catch { }
                try { max = dev.GainMax; gainInfoFound = true; } catch { }
                try { cur = dev.Gain; gainInfoFound = true; } catch { cur = min; }

                if (gainInfoFound)
                {
                    if (max < min)
                    {
                        (min, max) = (max, min);
                    }

                    cur = Math.Clamp(cur, min, max);

                    var gain = new CameraControl(ControlType.Gain, camera);
                    gain.ApplyDiscoveredState(min, max, 1, min, cur, false, false, string.Empty, ControlValueType.Int);
                    requestedGain = cur;
                    gainSupported = true;
                    controls.Add(gain);
                }
                else
                {
                    logger.Info("Alpaca camera does not expose readable gain metadata, gain control will not be shown.");
                }
            }
            catch (Exception ex)
            {
                logger.Info($"Alpaca camera gain initialization failed: {ex.Message}");
            }

            double minSec = 0.001, maxSec = 10;
            try
            {
                minSec = Math.Max(0.001, dev.ExposureMin);
                maxSec = Math.Max(minSec, Math.Min(dev.ExposureMax, 60));
            }
            catch { }
            double defSec = Math.Clamp(DefaultExposureSec, minSec, maxSec);
            requestedExposureSec = defSec;
            var exp = new CameraControl(ControlType.ExposureTime, camera);
            exp.ApplyDiscoveredState(minSec, maxSec, 0.01, defSec, defSec, false, false, string.Empty, ControlValueType.Int);
            controls.Add(exp);

            return controls;
        }

        private static int GetCurrentBin(AlpacaCamera dev)
        {
            try
            {
                var binX = Math.Max(1, (int)dev.BinX);
                var binY = Math.Max(1, (int)dev.BinY);
                return Math.Max(1, Math.Min(binX, binY));
            }
            catch
            {
                return 1;
            }
        }

        private static int GetMaxSupportedBin(AlpacaCamera dev)
        {
            var maxBin = 1;

            try
            {
                maxBin = Math.Max(1, Math.Min((int)dev.MaxBinX, (int)dev.MaxBinY));
            }
            catch
            {
            }

            if (maxBin > 1)
            {
                return maxBin;
            }

            var originalX = dev.BinX;
            var originalY = dev.BinY;

            try
            {
                for (short probe = 2; probe <= MaxFallbackProbeBin; probe++)
                {
                    try
                    {
                        dev.BinX = probe;
                        dev.BinY = probe;

                        if (dev.BinX == probe && dev.BinY == probe)
                        {
                            maxBin = probe;
                        }
                    }
                    catch
                    {
                        break;
                    }
                }
            }
            finally
            {
                try { dev.BinX = originalX; } catch { }
                try { dev.BinY = originalY; } catch { }
            }

            return Math.Max(1, maxBin);
        }

        private static void ApplyBinningAndFullFrameRoi(AlpacaCamera dev, int bin)
        {
            dev.BinX = (short)bin;
            dev.BinY = (short)bin;

            var effectiveBinX = Math.Max(1, (int)dev.BinX);
            var effectiveBinY = Math.Max(1, (int)dev.BinY);

            dev.StartX = 0;
            dev.StartY = 0;

            var targetNumX = Math.Max(1, dev.CameraXSize / effectiveBinX);
            var targetNumY = Math.Max(1, dev.CameraYSize / effectiveBinY);

            SetRoiDimensionWithFallback(value => dev.NumX = value, targetNumX, "NumX");
            SetRoiDimensionWithFallback(value => dev.NumY = value, targetNumY, "NumY");
        }

        private static void SetRoiDimensionWithFallback(Action<int> setter, int desiredValue, string propertyName)
        {
            try
            {
                setter(desiredValue);
            }
            catch (ASCOM.InvalidValueException ex) when (TryParseValidRange(ex.Message, out var min, out var max))
            {
                var clamped = Math.Clamp(desiredValue, min, max);
                if (clamped == desiredValue)
                {
                    throw;
                }

                logger.Warn($"Alpaca camera rejected {propertyName}={desiredValue}; retrying with clamped value {clamped} (valid range {min}..{max}).");
                setter(clamped);
            }
        }

        private static bool TryParseValidRange(string? message, out int min, out int max)
        {
            min = 0;
            max = 0;

            if (string.IsNullOrWhiteSpace(message))
            {
                return false;
            }

            var match = Regex.Match(
                message,
                @"valid\s+range\s+is:\s*(?<min>-?\d+)\s*to\s*(?<max>-?\d+)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

            if (!match.Success)
            {
                return false;
            }

            if (!int.TryParse(match.Groups["min"].Value, out min)
                || !int.TryParse(match.Groups["max"].Value, out max))
            {
                return false;
            }

            if (max < min)
            {
                (min, max) = (max, min);
            }

            return true;
        }

        private async Task CaptureLoop(AlpacaCamera dev, CancellationToken ct)
        {
            int appliedBin = 0;
            double appliedGain = double.NaN;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int bin;
                    double gain;
                    double expSec;
                    lock (sync)
                    {
                        bin = requestedBin;
                        gain = requestedGain;
                        expSec = requestedExposureSec;
                    }

                    if (bin != appliedBin)
                    {
                        try
                        {
                            ApplyBinningAndFullFrameRoi(dev, bin);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            logger.Warn($"Alpaca camera rejected binning/subframe settings, continuing with device defaults: {ex.Message}");
                        }
                        appliedBin = bin;
                    }

                    if (gainSupported && gain != appliedGain)
                    {
                        try
                        {
                            dev.Gain = (short)gain;
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            logger.Warn($"Alpaca camera failed to apply gain {gain}, continuing with device value: {ex.Message}");
                        }
                        appliedGain = gain;
                    }

                    PublishCaptureProgress(CameraCaptureStage.Exposing, 0, "Exposing", true);
                    dev.StartExposure(expSec, true);

                    var exposureStart = Stopwatch.GetTimestamp();
                    var nextExposureUpdate = exposureStart;
                    var exposureDurationSec = Math.Max(0.001, expSec);
                    var exposureUpdateStep = Math.Max(1L, Stopwatch.Frequency / 10L);

                    while (!dev.ImageReady)
                    {
                        var now = Stopwatch.GetTimestamp();
                        if (now >= nextExposureUpdate)
                        {
                            var elapsed = Stopwatch.GetElapsedTime(exposureStart, now).TotalSeconds;
                            var progress = Math.Clamp(elapsed / exposureDurationSec, 0d, 0.98d);
                            PublishCaptureProgress(CameraCaptureStage.Exposing, progress, $"Exposing {elapsed:F2}/{exposureDurationSec:F2}s", true);
                            nextExposureUpdate = now + exposureUpdateStep;
                        }

                        await Task.Delay(20, ct);
                    }

                    PublishCaptureProgress(CameraCaptureStage.Exposing, 1, "Exposure complete", true);

                    Array? imageData = null;
                    PublishCaptureProgress(CameraCaptureStage.Downloading, 0, "Downloading image", true);
                    for (int readAttempt = 1; readAttempt <= 3; readAttempt++)
                    {
                        PublishCaptureProgress(CameraCaptureStage.Downloading, (readAttempt - 1) / 3d, $"Downloading image (attempt {readAttempt}/3)", true);
                        try
                        {
                            var downloadStart = Stopwatch.GetTimestamp();
                            var readTask = Task.Run(() => dev.ImageArray as Array, ct);

                            while (!readTask.IsCompleted)
                            {
                                var elapsedMs = Stopwatch.GetElapsedTime(downloadStart).TotalMilliseconds;
                                var animated = ((elapsedMs % 1000d) / 1000d) * 0.8d + 0.1d;
                                PublishCaptureProgress(
                                    CameraCaptureStage.Downloading,
                                    animated,
                                    $"Downloading image (attempt {readAttempt}/3, {elapsedMs:F0} ms)",
                                    true);

                                await Task.Delay(50, ct);
                            }

                            imageData = await readTask;
                            break;
                        }
                        catch (ASCOM.DriverException ex) when (IsNoImageAvailable(ex) && readAttempt < 3)
                        {
                            await Task.Delay(30, ct);
                        }
                        catch (ASCOM.DriverException ex) when (IsNoImageAvailable(ex))
                        {
                            logger.Debug($"Alpaca camera reported image not available after exposure, skipping frame: {ex.Message}");
                        }
                    }

                    PublishCaptureProgress(CameraCaptureStage.Downloading, 1, "Download complete", true);

                    PublishCaptureProgress(CameraCaptureStage.Processing, 0, "Processing frame", true);
                    var frame = ToFrame(imageData);
                    PublishCaptureProgress(CameraCaptureStage.Processing, 1, "Frame ready", frame is not null);
                    if (frame is not null)
                    {
                        WeakReferenceMessenger.Default.Send(new CameraFrameMessage(frame));
                    }
                    else
                    {
                        PublishCaptureProgress(CameraCaptureStage.Idle, 0, "Waiting for next frame", false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                try { dev.AbortExposure(); } catch { }
                PublishCaptureProgress(CameraCaptureStage.Idle, 0, "Stopped", false);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Alpaca capture loop failed");
                PublishCaptureProgress(CameraCaptureStage.Idle, 0, "Capture failed", false);
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    WeakReferenceMessenger.Default.Send(new CameraStateMessage(CameraState.Stopped)));
            }
        }

        private static void PublishCaptureProgress(CameraCaptureStage stage, double progress, string status, bool isActive)
        {
            WeakReferenceMessenger.Default.Send(new CameraCaptureProgressMessage(
                new CameraCaptureProgress(stage, Math.Clamp(progress, 0, 1), status, isActive)));
        }

        private static bool IsNoImageAvailable(ASCOM.DriverException ex)
        {
            return ex.Message.Contains("No image available", StringComparison.OrdinalIgnoreCase);
        }

        private static CameraFrame? ToFrame(Array? data)
        {
            if (data is null)
            {
                return null;
            }

            return data switch
            {
                byte[,] p => ToFrame2D(p),
                short[,] p => ToFrame2D(p),
                ushort[,] p => ToFrame2D(p),
                int[,] p => ToFrame2D(p),
                uint[,] p => ToFrame2D(p),
                float[,] p => ToFrame2D(p),
                double[,] p => ToFrame2D(p),
                byte[,,] p => ToFrame3D(p),
                short[,,] p => ToFrame3D(p),
                ushort[,,] p => ToFrame3D(p),
                int[,,] p => ToFrame3D(p),
                uint[,,] p => ToFrame3D(p),
                float[,,] p => ToFrame3D(p),
                double[,,] p => ToFrame3D(p),
                _ => ToFrameFallback(data)
            };
        }

        private static CameraFrame? ToFrame2D<T>(T[,] data)
            where T : IConvertible
        {
            int width = data.GetLength(0);
            int height = data.GetLength(1);
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            int total = width * height;
            var values = new double[total];
            double min = double.MaxValue;
            double max = double.MinValue;
            int idx = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double value = data[x, y].ToDouble(null);
                    values[idx++] = value;
                    if (value < min) min = value;
                    if (value > max) max = value;
                }
            }

            return CreateNormalizedFrame(width, height, values, min, max);
        }

        private static CameraFrame? ToFrame3D<T>(T[,,] data)
            where T : IConvertible
        {
            int width = data.GetLength(0);
            int height = data.GetLength(1);
            int planes = data.GetLength(2);

            if (width <= 0 || height <= 0 || planes <= 0)
            {
                return null;
            }

            int total = width * height;
            var values = new double[total];
            double min = double.MaxValue;
            double max = double.MinValue;
            int idx = 0;
            double invPlanes = 1.0 / planes;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double sum = 0;
                    for (int p = 0; p < planes; p++)
                    {
                        sum += data[x, y, p].ToDouble(null);
                    }

                    double value = sum * invPlanes;
                    values[idx++] = value;
                    if (value < min) min = value;
                    if (value > max) max = value;
                }
            }

            return CreateNormalizedFrame(width, height, values, min, max);
        }

        private static CameraFrame? ToFrameFallback(Array data)
        {
            if (data.Rank is < 2 or > 3)
            {
                return null;
            }

            int width = data.GetLength(0);
            int height = data.GetLength(1);
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            int total = width * height;
            var values = new double[total];
            double min = double.MaxValue;
            double max = double.MinValue;
            int idx = 0;

            if (data.Rank == 2)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        double value = Convert.ToDouble(data.GetValue(x, y));
                        values[idx++] = value;
                        if (value < min) min = value;
                        if (value > max) max = value;
                    }
                }
            }
            else
            {
                int planes = data.GetLength(2);
                if (planes <= 0)
                {
                    return null;
                }

                double invPlanes = 1.0 / planes;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        double sum = 0;
                        for (int p = 0; p < planes; p++)
                        {
                            sum += Convert.ToDouble(data.GetValue(x, y, p));
                        }

                        double value = sum * invPlanes;
                        values[idx++] = value;
                        if (value < min) min = value;
                        if (value > max) max = value;
                    }
                }
            }

            return CreateNormalizedFrame(width, height, values, min, max);
        }

        private static CameraFrame CreateNormalizedFrame(int width, int height, double[] values, double min, double max)
        {
            var pixels = new byte[values.Length];
            double range = max - min;
            if (range <= 0)
            {
                return new CameraFrame(width, height, pixels);
            }

            double scale = 255.0 / range;
            for (int i = 0; i < values.Length; i++)
            {
                pixels[i] = (byte)((values[i] - min) * scale);
            }

            return new CameraFrame(width, height, pixels);
        }
    }
}
