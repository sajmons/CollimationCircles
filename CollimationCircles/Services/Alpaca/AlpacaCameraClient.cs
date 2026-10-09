using ASCOM.Alpaca.Clients;
using ASCOM.Alpaca.Discovery;
using ASCOM.Common.Alpaca;
using CollimationCircles.Messages;
using CollimationCircles.Models;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
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
                            dev.BinX = (short)bin;
                            dev.BinY = (short)bin;
                            dev.StartX = 0;
                            dev.StartY = 0;
                            dev.NumX = dev.CameraXSize;
                            dev.NumY = dev.CameraYSize;
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

                    dev.StartExposure(expSec, true);

                    while (!dev.ImageReady)
                    {
                        await Task.Delay(20, ct);
                    }

                    Array? imageData = null;
                    for (int readAttempt = 1; readAttempt <= 3; readAttempt++)
                    {
                        try
                        {
                            imageData = dev.ImageArray as Array;
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

                    var frame = ToFrame(imageData);
                    if (frame is not null)
                    {
                        WeakReferenceMessenger.Default.Send(new CameraFrameMessage(frame));
                    }
                }
            }
            catch (OperationCanceledException)
            {
                try { dev.AbortExposure(); } catch { }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Alpaca capture loop failed");
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    WeakReferenceMessenger.Default.Send(new CameraStateMessage(CameraState.Stopped)));
            }
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

            int width, height;
            Func<int, int, double> get;

            if (data.Rank == 2)
            {
                width = data.GetLength(0);
                height = data.GetLength(1);
                if (data is int[,] ints)
                {
                    get = (x, y) => ints[x, y];
                }
                else
                {
                    get = (x, y) => Convert.ToDouble(data.GetValue(x, y));
                }
            }
            else if (data.Rank == 3)
            {
                width = data.GetLength(0);
                height = data.GetLength(1);
                int planes = data.GetLength(2);
                get = (x, y) =>
                {
                    double s = 0;
                    for (int p = 0; p < planes; p++)
                    {
                        s += Convert.ToDouble(data.GetValue(x, y, p));
                    }
                    return s / planes;
                };
            }
            else
            {
                return null;
            }

            if (width <= 0 || height <= 0)
            {
                return null;
            }

            var values = new double[width * height];
            double min = double.MaxValue, max = double.MinValue;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double v = get(x, y);
                    values[y * width + x] = v;
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
            }

            double range = max - min;
            var pixels = new byte[values.Length];
            if (range > 0)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    pixels[i] = (byte)((values[i] - min) * 255.0 / range);
                }
            }

            return new CameraFrame(width, height, pixels);
        }
    }
}
