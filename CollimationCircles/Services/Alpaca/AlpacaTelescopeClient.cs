using CollimationCircles.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CollimationCircles.Services.Alpaca
{
    internal class AlpacaTelescopeClient
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private static readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

        private const int ClientId = 7342;

        private readonly SemaphoreSlim transactionLock = new(1, 1);

        private string serverAddress = string.Empty;
        private int serverPort;
        private TelescopeMount? selectedMount;
        private int clientTransactionId;
        private CancellationTokenSource? pulseGuideCancellationTokenSource;
        private Task? pulseGuideTask;
        private readonly Dictionary<int, (double Min, double Max)> axisRateLimits = new();

        public async Task<List<TelescopeMount>> DiscoverMountsAsync(string address, int port)
        {
            serverAddress = address.Trim();
            serverPort = port;

            var endpoint = BuildManagementUri(address, port);
            using var response = await httpClient.GetAsync(endpoint).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

            var mountList = new List<TelescopeMount>();

            if (!TryGetDeviceArray(json.RootElement, out var devicesElement))
            {
                return mountList;
            }

            foreach (var item in devicesElement.EnumerateArray())
            {
                var deviceType = TryReadString(item, "DeviceType");
                if (!string.Equals(deviceType, "Telescope", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!TryReadInt(item, "DeviceNumber", out var deviceNumber))
                {
                    continue;
                }

                var name = TryReadString(item, "DeviceName");
                var uniqueId = TryReadString(item, "UniqueID") ?? TryReadString(item, "UniqueId") ?? string.Empty;

                mountList.Add(new TelescopeMount
                {
                    DeviceNumber = deviceNumber,
                    Name = string.IsNullOrWhiteSpace(name) ? $"Mount {deviceNumber}" : name,
                    UniqueId = uniqueId
                });
            }

            var result = mountList.OrderBy(m => m.Name).ToList();
            logger.Info($"Discovered {result.Count} telescope mount(s) at {address}:{port}");
            return result;
        }

        public void SelectMount(TelescopeMount mount)
        {
            selectedMount = mount;
            axisRateLimits.Clear();
        }

        public void ClearSelectedMount()
        {
            selectedMount = null;
        }

        public async Task StartMotion(TelescopeDirection direction, double speed)
        {
            EnsureMountSelected();

            CancelPulseGuideMotion();

            await EnsureConnectedAsync().ConfigureAwait(false);

            if (await GetParkedAsync().ConfigureAwait(false))
            {
                throw new InvalidOperationException("Cannot start motion while the mount is parked.");
            }

            var moveRate = Math.Abs(speed);

            var northSouthRate = direction switch
            {
                TelescopeDirection.Up or TelescopeDirection.UpLeft or TelescopeDirection.UpRight => moveRate,
                TelescopeDirection.Down or TelescopeDirection.DownLeft or TelescopeDirection.DownRight => -moveRate,
                _ => 0d
            };

            var eastWestRate = direction switch
            {
                TelescopeDirection.Right or TelescopeDirection.UpRight or TelescopeDirection.DownRight => moveRate,
                TelescopeDirection.Left or TelescopeDirection.UpLeft or TelescopeDirection.DownLeft => -moveRate,
                _ => 0d
            };

            if (eastWestRate != 0d && northSouthRate != 0d)
            {
                if (await TryStartDiagonalMoveAxisMotionAsync(eastWestRate, northSouthRate).ConfigureAwait(false))
                {
                    return;
                }

                if (await CanPulseGuideAsync().ConfigureAwait(false))
                {
                    StartPulseGuideMotion(eastWestRate, northSouthRate, moveRate);
                    return;
                }

                throw new InvalidOperationException("Selected mount does not support MoveAxis or PulseGuide motion required for diagonal movement.");
            }

            if (eastWestRate != 0d)
            {
                if (await TryStartSingleAxisMoveAxisMotionAsync(eastWestRate, preferredAxis: 0).ConfigureAwait(false))
                {
                    return;
                }

                if (await CanPulseGuideAsync().ConfigureAwait(false))
                {
                    StartPulseGuideMotion(eastWestRate, northSouthRate, moveRate);
                    return;
                }

                throw new InvalidOperationException("Selected mount does not support MoveAxis or PulseGuide for east-west motion.");
            }

            if (northSouthRate != 0d)
            {
                if (await TryStartSingleAxisMoveAxisMotionAsync(northSouthRate, preferredAxis: 1).ConfigureAwait(false))
                {
                    return;
                }

                if (await CanPulseGuideAsync().ConfigureAwait(false))
                {
                    StartPulseGuideMotion(eastWestRate, northSouthRate, moveRate);
                    return;
                }

                throw new InvalidOperationException("Selected mount does not support MoveAxis or PulseGuide for north-south motion.");
            }
        }

        public async Task StopMotion()
        {
            EnsureMountSelected();

            CancelPulseGuideMotion();

            try
            {
                await SendPutAsync("abortslew").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "AbortSlew failed, falling back to MoveAxis=0 on each axis.");
            }

            await TryStopAxisAsync(0).ConfigureAwait(false);
            await TryStopAxisAsync(1).ConfigureAwait(false);
        }

        private async Task<bool> CanPulseGuideAsync()
        {
            try
            {
                return await SendGetBoolAsync("canpulseguide").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "Could not read CanPulseGuide capability.");
                return false;
            }
        }

        private void StartPulseGuideMotion(double eastWestRate, double northSouthRate, double speed)
        {
            CancelPulseGuideMotion();

            var directions = GetPulseGuideDirections(eastWestRate, northSouthRate);
            if (directions.Count == 0)
            {
                return;
            }

            var pulseDurationMs = GetPulseDurationMilliseconds(speed);
            var cancellationTokenSource = new CancellationTokenSource();
            pulseGuideCancellationTokenSource = cancellationTokenSource;
            var token = cancellationTokenSource.Token;

            pulseGuideTask = Task.Run(async () =>
            {
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        foreach (var direction in directions)
                        {
                            await PulseGuideAsync(direction, pulseDurationMs).ConfigureAwait(false);

                            if (token.IsCancellationRequested)
                            {
                                break;
                            }
                        }

                        await Task.Delay(20, token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "PulseGuide motion loop failed.");
                }
            }, token);

            logger.Info($"Using PulseGuide fallback for motion. duration={pulseDurationMs}ms directions={string.Join(',', directions)}");
        }

        private void CancelPulseGuideMotion()
        {
            var cancellationTokenSource = Interlocked.Exchange(ref pulseGuideCancellationTokenSource, null);
            if (cancellationTokenSource is not null)
            {
                cancellationTokenSource.Cancel();
                cancellationTokenSource.Dispose();
            }

            Interlocked.Exchange(ref pulseGuideTask, null);
        }

        private async Task PulseGuideAsync(int direction, int durationMilliseconds)
        {
            await SendPutAsync("pulseguide", ("Direction", direction.ToString()), ("Duration", durationMilliseconds.ToString())).ConfigureAwait(false);
        }

        private static List<int> GetPulseGuideDirections(double eastWestRate, double northSouthRate)
        {
            var directions = new List<int>(2);

            if (northSouthRate > 0d)
            {
                directions.Add(0);
            }
            else if (northSouthRate < 0d)
            {
                directions.Add(1);
            }

            if (eastWestRate > 0d)
            {
                directions.Add(2);
            }
            else if (eastWestRate < 0d)
            {
                directions.Add(3);
            }

            return directions;
        }

        private static int GetPulseDurationMilliseconds(double speed)
        {
            var scaled = (int)Math.Round(150d * Math.Clamp(speed, 0.1d, 4d));
            return Math.Clamp(scaled, 50, 600);
        }

        public async Task<bool> GetTrackingAsync()
        {
            EnsureMountSelected();
            return await SendGetBoolAsync("tracking").ConfigureAwait(false);
        }

        public async Task SetTrackingAsync(bool tracking)
        {
            EnsureMountSelected();
            await SendPutAsync("tracking", ("Tracking", tracking ? "true" : "false")).ConfigureAwait(false);
        }

        public async Task<bool> GetParkedAsync()
        {
            EnsureMountSelected();
            return await SendGetBoolAsync("atpark").ConfigureAwait(false);
        }

        public async Task ParkAsync()
        {
            EnsureMountSelected();
            await SendPutAsync("park").ConfigureAwait(false);
        }

        public async Task UnparkAsync()
        {
            EnsureMountSelected();
            await SendPutAsync("unpark").ConfigureAwait(false);
        }

        private async Task MoveAxisAsync(int axis, double rate)
        {
            var clampedRate = await ClampRateAsync(axis, rate).ConfigureAwait(false);
            await SendPutAsync("moveaxis", ("Axis", axis.ToString()), ("Rate", clampedRate.ToString(System.Globalization.CultureInfo.InvariantCulture))).ConfigureAwait(false);
        }

        private async Task EnsureConnectedAsync()
        {
            try
            {
                if (await SendGetBoolAsync("connected").ConfigureAwait(false))
                {
                    return;
                }

                await SendPutAsync("connected", ("Connected", "true")).ConfigureAwait(false);
                logger.Info("Telescope device was not connected; connected it.");
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "Could not verify/establish telescope connection.");
            }
        }

        private async Task<double> ClampRateAsync(int axis, double rate)
        {
            if (rate == 0d)
            {
                return 0d;
            }

            if (!axisRateLimits.TryGetValue(axis, out var limits))
            {
                limits = await ReadAxisRateLimitsAsync(axis).ConfigureAwait(false);
                axisRateLimits[axis] = limits;
            }

            if (limits.Max <= 0d)
            {
                return rate;
            }

            var clamped = Math.Clamp(Math.Abs(rate), limits.Min, limits.Max);
            if (clamped != Math.Abs(rate))
            {
                logger.Debug($"MoveAxis rate {rate} adjusted to {Math.Sign(rate) * clamped} for axis {axis} (limits {limits.Min}-{limits.Max}).");
            }

            return Math.Sign(rate) * clamped;
        }

        private async Task<(double Min, double Max)> ReadAxisRateLimitsAsync(int axis)
        {
            try
            {
                var transactionId = await NextClientTransactionIdAsync().ConfigureAwait(false);
                var uri = BuildTelescopeUri("axisrates", transactionId, ("Axis", axis.ToString()));
                using var response = await httpClient.GetAsync(uri).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var json = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
                EnsureSuccess(json.RootElement, "axisrates");

                if (TryGetProperty(json.RootElement, "Value", out var value) && value.ValueKind == JsonValueKind.Array)
                {
                    var min = double.MaxValue;
                    var max = 0d;

                    foreach (var item in value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Object
                            && TryGetProperty(item, "Minimum", out var minElement) && minElement.TryGetDouble(out var itemMin)
                            && TryGetProperty(item, "Maximum", out var maxElement) && maxElement.TryGetDouble(out var itemMax))
                        {
                            min = Math.Min(min, itemMin);
                            max = Math.Max(max, itemMax);
                        }
                    }

                    if (max > 0d)
                    {
                        logger.Info($"Axis {axis} rate limits: {min}-{max} deg/s");
                        return (min, max);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Debug(ex, $"Could not read AxisRates for axis {axis}.");
            }

            return (0d, 0d);
        }

        private async Task<bool> SendGetBoolAsync(string endpoint)
        {
            return await SendGetBoolAsync(endpoint, Array.Empty<(string Name, string Value)>()).ConfigureAwait(false);
        }

        private async Task<bool> SendGetBoolAsync(string endpoint, params (string Name, string Value)[] parameters)
        {
            var transactionId = await NextClientTransactionIdAsync().ConfigureAwait(false);
            var uri = BuildTelescopeUri(endpoint, transactionId, parameters);
            using var response = await httpClient.GetAsync(uri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
            EnsureSuccess(json.RootElement, endpoint);

            return TryReadBool(json.RootElement, "Value", out var value) && value;
        }

        private async Task SendPutAsync(string endpoint, params (string Name, string Value)[] parameters)
        {
            var transactionId = await NextClientTransactionIdAsync().ConfigureAwait(false);
            var uri = BuildTelescopeUri(endpoint, transactionId);

            if (string.Equals(endpoint, "moveaxis", StringComparison.OrdinalIgnoreCase))
            {
                var axis = parameters.FirstOrDefault(p => string.Equals(p.Name, "Axis", StringComparison.OrdinalIgnoreCase)).Value;
                var rate = parameters.FirstOrDefault(p => string.Equals(p.Name, "Rate", StringComparison.OrdinalIgnoreCase)).Value;
                logger.Debug($"Alpaca PUT moveaxis txId={transactionId} axis={axis} rate={rate} uri={uri}");
            }

            var payload = new List<KeyValuePair<string, string>>(parameters.Length + 2)
            {
                new("ClientID", ClientId.ToString()),
                new("ClientTransactionID", transactionId.ToString())
            };

            foreach (var parameter in parameters)
            {
                payload.Add(new KeyValuePair<string, string>(parameter.Name, parameter.Value));
            }

            using var content = new FormUrlEncodedContent(payload);
            using var response = await httpClient.PutAsync(uri, content).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);
            EnsureSuccess(json.RootElement, endpoint);
        }

        private async Task<bool> CanMoveAxisAsync(int axis)
        {
            try
            {
                return await SendGetBoolAsync("canmoveaxis", ("Axis", axis.ToString())).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.Debug(ex, $"Could not read CanMoveAxis for axis {axis}; proceeding with MoveAxis attempt.");
                return true;
            }
        }

        private async Task<bool> TryStartSingleAxisMoveAxisMotionAsync(double rate, int preferredAxis)
        {
            if (await TryMoveAxisAsync(preferredAxis, rate).ConfigureAwait(false))
            {
                return true;
            }

            var fallbackAxis = preferredAxis == 0 ? 1 : 0;
            if (await TryMoveAxisAsync(fallbackAxis, rate).ConfigureAwait(false))
            {
                logger.Warn($"MoveAxis failed on preferred axis {preferredAxis}; using axis {fallbackAxis} as fallback.");
                return true;
            }

            return false;
        }

        private async Task<bool> TryStartDiagonalMoveAxisMotionAsync(double eastWestRate, double northSouthRate)
        {
            if (await TryMoveAxisAsync(0, eastWestRate).ConfigureAwait(false))
            {
                if (await TryMoveAxisAsync(1, northSouthRate).ConfigureAwait(false))
                {
                    return true;
                }

                await TryStopAxisAsync(0).ConfigureAwait(false);
            }

            if (await TryMoveAxisAsync(1, eastWestRate).ConfigureAwait(false))
            {
                if (await TryMoveAxisAsync(0, northSouthRate).ConfigureAwait(false))
                {
                    return true;
                }

                await TryStopAxisAsync(1).ConfigureAwait(false);
            }

            return false;
        }

        private async Task<bool> TryMoveAxisAsync(int axis, double rate)
        {
            try
            {
                await MoveAxisAsync(axis, rate).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                logger.Debug(ex, $"MoveAxis failed on axis {axis} with rate {rate}.");
                return false;
            }
        }

        private async Task<int?> ResolveSingleAxisAsync(int preferredAxis)
        {
            if (await CanMoveAxisAsync(preferredAxis).ConfigureAwait(false))
            {
                return preferredAxis;
            }

            var fallbackAxis = preferredAxis == 0 ? 1 : 0;
            if (await CanMoveAxisAsync(fallbackAxis).ConfigureAwait(false))
            {
                logger.Warn($"MoveAxis not supported on preferred axis {preferredAxis}; using axis {fallbackAxis} as fallback.");
                return fallbackAxis;
            }

            return null;
        }

        private async Task TryStopAxisAsync(int axis)
        {
            try
            {
                await MoveAxisAsync(axis, 0d).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.Debug(ex, $"Failed to stop MoveAxis on axis {axis}.");
            }
        }

        private Uri BuildTelescopeUri(string endpoint, int transactionId, params (string Name, string Value)[] parameters)
        {
            EnsureMountSelected();

            var host = NormalizeHost(serverAddress);

            var query = new List<string>(parameters.Length + 2)
            {
                $"ClientID={ClientId}",
                $"ClientTransactionID={transactionId}"
            };

            foreach (var parameter in parameters)
            {
                query.Add($"{Uri.EscapeDataString(parameter.Name)}={Uri.EscapeDataString(parameter.Value)}");
            }

            var queryString = string.Join("&", query);
            return new Uri($"http://{host}:{serverPort}/api/v1/telescope/{selectedMount!.DeviceNumber}/{endpoint}?{queryString}");
        }

        private static Uri BuildManagementUri(string address, int port)
        {
            var host = NormalizeHost(address);
            return new Uri($"http://{host}:{port}/management/v1/configureddevices");
        }

        private static string NormalizeHost(string address)
        {
            var host = address.Trim().Trim('[', ']');
            if (IPAddress.TryParse(host, out var ip) && ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            {
                return $"[{host}]";
            }

            return host;
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

        private static void EnsureSuccess(JsonElement root, string endpoint)
        {
            if (!TryReadInt(root, "ErrorNumber", out var errorNumber) || errorNumber == 0)
            {
                return;
            }

            var errorMessage = TryReadString(root, "ErrorMessage") ?? "Unknown Alpaca error";
            throw new InvalidOperationException($"Alpaca call '{endpoint}' failed: {errorNumber} {errorMessage}");
        }

        private static bool TryReadInt(JsonElement element, string propertyName, out int value)
        {
            if (TryGetProperty(element, propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value))
            {
                return true;
            }

            value = default;
            return false;
        }

        private static bool TryReadBool(JsonElement element, string propertyName, out bool value)
        {
            if (TryGetProperty(element, propertyName, out var property) && property.ValueKind == JsonValueKind.True)
            {
                value = true;
                return true;
            }

            if (TryGetProperty(element, propertyName, out property) && property.ValueKind == JsonValueKind.False)
            {
                value = false;
                return true;
            }

            value = default;
            return false;
        }

        private static string? TryReadString(JsonElement element, string propertyName)
        {
            if (TryGetProperty(element, propertyName, out var property) && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }

            return null;
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

        private async Task<int> NextClientTransactionIdAsync()
        {
            await transactionLock.WaitAsync().ConfigureAwait(false);
            try
            {
                return ++clientTransactionId;
            }
            finally
            {
                transactionLock.Release();
            }
        }

        private void EnsureMountSelected()
        {
            if (selectedMount is null)
            {
                throw new InvalidOperationException("No telescope mount is selected.");
            }
        }
    }
}
