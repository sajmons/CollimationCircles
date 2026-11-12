using CollimationCircles.Messages;
using CollimationCircles.Models;
using Avalonia.Threading;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using OpenCvSharp;
using System;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    internal class CameraService : ICameraService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private const string DefaultAlpacaHost = "127.0.0.1";
        private const int DefaultAlpacaPort = 11111;

        private readonly object frameSync = new();
        private Mat? latestFrame;

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

            WeakReferenceMessenger.Default.Register<MatMessage>(this, (r, m) =>
            {
                var service = (CameraService)r;
                lock (service.frameSync)
                {
                    service.latestFrame?.Dispose();
                    service.latestFrame = m.Value.Clone();
                }
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

        public Mat? GetLatestFrame()
        {
            lock (frameSync)
            {
                return latestFrame is null || latestFrame.Empty() ? null : latestFrame.Clone();
            }
        }

        public Task<byte[]?> TakeSnapshotImageAsync()
        {
            lock (frameSync)
            {
                if (latestFrame is null || latestFrame.Empty())
                {
                    return Task.FromResult<byte[]?>(null);
                }

                using var snapshot = latestFrame.Clone();
                if (snapshot.Empty())
                {
                    return Task.FromResult<byte[]?>(null);
                }

                Cv2.ImEncode(".png", snapshot, out var bytes);
                return Task.FromResult<byte[]?>(bytes);
            }
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
    }
}
