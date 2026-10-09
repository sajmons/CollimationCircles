using CollimationCircles.Models;
using CollimationCircles.Services.Alpaca;
using CommunityToolkit.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    internal class CameraControlService : ICameraControlService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly AlpacaCameraClient alpacaCamera = new();

        public void Set(ControlType controlName, double value, Camera camera)
        {
            Guard.IsNotNull(camera);
            Guard.IsTrue(camera.IsPlaying);

            logger.Info($"Dispatching camera control set: camera='{camera.Name}', api={camera.APIType}, control={controlName}, value={value}");

            if (camera.APIType is APIType.Alpaca)
            {
                alpacaCamera.SetControl(camera, controlName, value);
            }
        }

        public bool IsServerConnected => alpacaCamera.IsConnected;

        public async Task<List<Camera>> ConnectServer(string address, int port)
        {
            return await alpacaCamera.Connect(address, port);
        }

        public async Task DisconnectServer()
        {
            await alpacaCamera.Disconnect();
        }

        public async Task StartCamera(Camera camera)
        {
            if (camera.APIType is APIType.Alpaca)
            {
                try
                {
                    await alpacaCamera.Start(camera);
                    return;
                }
                catch (Exception ex) when (IsDeviceNotFoundError(ex))
                {
                    logger.Warn(ex, $"Alpaca device not found for '{camera.Name}' (DeviceNumber={camera.DeviceNumber}) at {camera.ServerAddress}:{camera.ServerPort}. Attempting rediscovery and retry.");

                    await RediscoverAndRetargetSelectedCameraAsync(camera);

                    await alpacaCamera.Start(camera);
                    return;
                }
            }
        }

        private async Task RediscoverAndRetargetSelectedCameraAsync(Camera selected)
        {
            var cameras = await alpacaCamera.Connect(selected.ServerAddress, selected.ServerPort);
            if (cameras.Count == 0)
            {
                throw new InvalidOperationException($"Rediscovery found no Alpaca cameras at {selected.ServerAddress}:{selected.ServerPort}.");
            }

            var candidate = cameras.FirstOrDefault(c => string.Equals(c.Name, selected.Name, StringComparison.OrdinalIgnoreCase))
                            ?? cameras.FirstOrDefault(c => c.DeviceNumber == selected.DeviceNumber)
                            ?? cameras.First();

            var previousName = selected.Name;
            var previousDeviceNumber = selected.DeviceNumber;

            selected.Name = candidate.Name;
            selected.DeviceNumber = candidate.DeviceNumber;

            logger.Info($"Retargeted Alpaca camera from '{previousName}'#{previousDeviceNumber} to '{selected.Name}'#{selected.DeviceNumber} at {selected.ServerAddress}:{selected.ServerPort}.");
        }

        private static bool IsDeviceNotFoundError(Exception ex)
        {
            var message = ex.ToString();

            return message.Contains("ErrorNumber\":1003", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("Device not found", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("HTTP Completion Status: NotFound", StringComparison.OrdinalIgnoreCase);
        }

        public async Task StopCamera(Camera camera)
        {
            if (camera.APIType is APIType.Alpaca)
            {
                await alpacaCamera.Stop(camera);
            }
        }

        public void SetAuto(ControlType propertyname, bool isAuto, Camera camera)
        {
            throw new NotImplementedException();
        }
    }
}
