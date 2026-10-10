using CollimationCircles.Messages;
using CollimationCircles.Models;
using Avalonia.Threading;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    internal class CameraService : ICameraService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private const string DefaultAlpacaHost = "127.0.0.1";
        private const int DefaultAlpacaPort = 11111;

        public bool IsAvailable { get; private set; }
        public string FullAddress { get; set; } = string.Empty;
        public bool IsPlaying { get; private set; }

        public CameraService()
        {
            // Keep IsPlaying in sync when a camera stops on its own (e.g. capture loop error).
            WeakReferenceMessenger.Default.Register<CameraStateMessage>(this, (r, m) =>
            {
                ((CameraService)r).IsPlaying = m.Value == CameraState.Playing;
            });
        }

        private static void SendCameraStateOnUIThread(CameraState state)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                WeakReferenceMessenger.Default.Send(new CameraStateMessage(state));
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                WeakReferenceMessenger.Default.Send(new CameraStateMessage(state));
            });
        }

        public async Task Play(Camera camera)
        {
            Guard.IsNotNull(camera);

            SendCameraStateOnUIThread(CameraState.Opening);
            try
            {
                await Ioc.Default.GetRequiredService<ICameraControlService>().StartCamera(camera);
                IsPlaying = true;
                SendCameraStateOnUIThread(CameraState.Playing);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to start camera");
                IsPlaying = false;
                SendCameraStateOnUIThread(CameraState.Stopped);
            }
        }

        public async Task Stop(Camera camera)
        {
            Guard.IsNotNull(camera);

            await Ioc.Default.GetRequiredService<ICameraControlService>().StopCamera(camera);
            IsPlaying = false;
            SendCameraStateOnUIThread(CameraState.Stopped);
        }

        public string DefaultAddress(Camera camera)
        {
            Guard.IsNotNull(camera);

            if (camera.APIType == APIType.Alpaca)
            {
                var host = string.IsNullOrWhiteSpace(camera.ServerAddress)
                    ? DefaultAlpacaHost
                    : camera.ServerAddress.Trim();
                var port = camera.ServerPort > 0 ? camera.ServerPort : DefaultAlpacaPort;
                FullAddress = $"http://{host}:{port}";
            }
            else
            {
                FullAddress = string.Empty;
            }

            return FullAddress;
        }

        public async Task<byte[]?> TakeSnapshotImageAsync()
        {
            try
            {

            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to capture live snapshot data");
            }

            return null;
        }        
    }
}
