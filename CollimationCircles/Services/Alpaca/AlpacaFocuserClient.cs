using CollimationCircles.Extensions;
using CollimationCircles.Models;
using CommunityToolkit.Mvvm.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CollimationCircles.Services.Alpaca
{
    internal class AlpacaFocuserClient
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private static readonly HttpClient httpClient = new(new SocketsHttpHandler { MaxConnectionsPerServer = 1 }) { Timeout = TimeSpan.FromSeconds(5) };

        private const int ClientId = 7342;
        private static readonly TimeSpan MovePollInterval = TimeSpan.FromMilliseconds(200);
        private static readonly TimeSpan MoveTimeout = TimeSpan.FromSeconds(120);

        private int clientTransactionId;
        private string serverAddress = string.Empty;
        private int serverPort;
        private Focuser? selectedFocuser;
        private bool isAbsolute = true;
        private int maxStep = int.MaxValue;
        private int maxIncrement = int.MaxValue;

        public async Task<List<Focuser>> DiscoverFocusersAsync(string address, int port)
        {
            serverAddress = address.Trim();
            serverPort = port;

            var endpoint = new Uri($"http://{NormalizeHost(address)}:{port}/management/v1/configureddevices");
            using var response = await httpClient.GetAsync(endpoint).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

            var focusers = new List<Focuser>();

            if (!TryGetDeviceArray(json.RootElement, out var devicesElement))
            {
                return focusers;
            }

            foreach (var item in devicesElement.EnumerateArray())
            {
                if (!string.Equals(TryReadString(item, "DeviceType"), "Focuser", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!TryReadInt(item, "DeviceNumber", out var deviceNumber))
                {
                    continue;
                }

                var name = TryReadString(item, "DeviceName");
                focusers.Add(new Focuser
                {
                    DeviceNumber = deviceNumber,
                    Name = string.IsNullOrWhiteSpace(name) ? Ioc.Default.GetRequiredService<IResourceService>().TryGetString("DefaultFocuserName").F(deviceNumber) : name,
                    UniqueId = TryReadString(item, "UniqueID") ?? TryReadString(item, "UniqueId") ?? string.Empty
                });
            }

            var result = focusers.OrderBy(f => f.Name).ToList();
            logger.Info($"Discovered {result.Count} focuser(s) at {address}:{port}");
            return result;
        }

        /// <summary>
        /// Selects the focuser, connects it and reads its capabilities. Returns the current position when the focuser reports one.
        /// </summary>
        public async Task<int?> SelectFocuserAsync(Focuser focuser)
        {
            selectedFocuser = focuser;
            isAbsolute = true;
            maxStep = int.MaxValue;
            maxIncrement = int.MaxValue;

            await EnsureConnectedAsync().ConfigureAwait(false);

            isAbsolute = await TryGetBoolAsync("absolute").ConfigureAwait(false) ?? true;
            maxStep = await TryGetIntAsync("maxstep").ConfigureAwait(false) ?? int.MaxValue;
            maxIncrement = await TryGetIntAsync("maxincrement").ConfigureAwait(false) ?? int.MaxValue;
            logger.Info($"Focuser '{focuser.Name}': absolute={isAbsolute}, maxStep={maxStep}, maxIncrement={maxIncrement}");

            return await GetPositionAsync().ConfigureAwait(false);
        }

        public void ClearSelectedFocuser()
        {
            selectedFocuser = null;
        }

        public async Task<int?> GetPositionAsync()
        {
            EnsureFocuserSelected();
            return isAbsolute ? await TryGetIntAsync("position").ConfigureAwait(false) : null;
        }

        /// <summary>
        /// Moves the focuser by a number of steps (negative values move towards zero) and waits until the movement finishes.
        /// Returns the resulting position when the focuser reports one.
        /// </summary>
        public async Task<int?> MoveByAsync(int steps)
        {
            EnsureFocuserSelected();

            if (steps == 0)
            {
                return await GetPositionAsync().ConfigureAwait(false);
            }

            int target;
            if (isAbsolute)
            {
                var current = await GetIntAsync("position").ConfigureAwait(false);
                target = (int)Math.Clamp((long)current + steps, 0L, maxStep);
                if (target == current)
                {
                    return current;
                }
            }
            else
            {
                target = Math.Clamp(steps, -maxIncrement, maxIncrement);
            }

            await SendPutAsync("move", ("Position", target.ToString())).ConfigureAwait(false);
            await WaitForMoveEndAsync().ConfigureAwait(false);

            return await GetPositionAsync().ConfigureAwait(false);
        }

        public async Task HaltAsync()
        {
            EnsureFocuserSelected();
            await SendPutAsync("halt").ConfigureAwait(false);
        }

        private async Task WaitForMoveEndAsync()
        {
            var started = DateTime.UtcNow;
            while (await GetBoolAsync("ismoving").ConfigureAwait(false))
            {
                if (DateTime.UtcNow - started > MoveTimeout)
                {
                    throw new TimeoutException("Focuser movement did not finish in time.");
                }

                await Task.Delay(MovePollInterval).ConfigureAwait(false);
            }
        }

        private async Task EnsureConnectedAsync()
        {
            try
            {
                if (await GetBoolAsync("connected").ConfigureAwait(false))
                {
                    return;
                }

                await SendPutAsync("connected", ("Connected", "true")).ConfigureAwait(false);
                logger.Info("Focuser device was not connected; connected it.");
            }
            catch (Exception ex)
            {
                logger.Debug(ex, "Could not verify/establish focuser connection.");
            }
        }

        private async Task<JsonDocument> SendGetAsync(string endpoint)
        {
            var uri = BuildFocuserUri(endpoint, NextClientTransactionId());
            using var response = await httpClient.GetAsync(uri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            var json = await JsonDocument.ParseAsync(stream).ConfigureAwait(false);

            try
            {
                EnsureSuccess(json.RootElement, endpoint);
                return json;
            }
            catch
            {
                json.Dispose();
                throw;
            }
        }

        private async Task<bool> GetBoolAsync(string endpoint)
        {
            using var json = await SendGetAsync(endpoint).ConfigureAwait(false);
            return TryGetProperty(json.RootElement, "Value", out var value) && value.ValueKind == JsonValueKind.True;
        }

        private async Task<int> GetIntAsync(string endpoint)
        {
            using var json = await SendGetAsync(endpoint).ConfigureAwait(false);
            if (TryGetProperty(json.RootElement, "Value", out var value) && value.TryGetInt32(out var result))
            {
                return result;
            }

            throw new InvalidOperationException($"Alpaca call '{endpoint}' returned no integer value.");
        }

        private async Task<bool?> TryGetBoolAsync(string endpoint)
        {
            try
            {
                return await GetBoolAsync(endpoint).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.Debug(ex, $"Could not read focuser property {endpoint}.");
                return null;
            }
        }

        private async Task<int?> TryGetIntAsync(string endpoint)
        {
            try
            {
                return await GetIntAsync(endpoint).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.Debug(ex, $"Could not read focuser property {endpoint}.");
                return null;
            }
        }

        private async Task SendPutAsync(string endpoint, params (string Name, string Value)[] parameters)
        {
            var transactionId = NextClientTransactionId();
            var uri = BuildFocuserUri(endpoint, transactionId, includeQuery: false);

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

        private Uri BuildFocuserUri(string endpoint, int transactionId, bool includeQuery = true)
        {
            EnsureFocuserSelected();

            var baseUri = $"http://{NormalizeHost(serverAddress)}:{serverPort}/api/v1/focuser/{selectedFocuser!.DeviceNumber}/{endpoint}";
            return new Uri(includeQuery ? $"{baseUri}?ClientID={ClientId}&ClientTransactionID={transactionId}" : baseUri);
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
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        value = property.Value;
                        return true;
                    }
                }
            }

            value = default;
            return false;
        }

        private int NextClientTransactionId() => Interlocked.Increment(ref clientTransactionId);

        private void EnsureFocuserSelected()
        {
            if (selectedFocuser is null)
            {
                throw new InvalidOperationException("No focuser is selected.");
            }
        }
    }
}
