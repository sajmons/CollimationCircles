using CollimationCircles.Messages;
using CollimationCircles.Models;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using LibVLCSharp.Shared;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    internal class LibVLCService : ILibVLCService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private readonly LibVLC libVLC;

        private string protocol = string.Empty;
        private string address = string.Empty;
        private string port = string.Empty;
        private string pathAndQuery = string.Empty;
        private const string rpiPort = "55555";

        public string FullAddress { get; set; } = string.Empty;
        public MediaPlayer MediaPlayer { get; }
        
        public LibVLCService()
        {
            // https://wiki.videolan.org/VLC_command-line_help/

            string[] libVLCOptions = [
                //$"--width=320",
                //$"--height=240",
                //$"--zoom=1.5",
                //$"--log-verbose=0"
                //"--video-filter=adjust{contrast=1.0,brightness=1.0,hue=0,saturation=1.0,gamma=1.0}"
            ];

            libVLC = new(libVLCOptions);

            MediaPlayer = new(libVLC)
            {
                FileCaching = 0,
                NetworkCaching = 0,
                EnableHardwareDecoding = true
            };

            MediaPlayer.Opening += (sender, e) => WeakReferenceMessenger.Default.Send(new CameraStateMessage(CameraState.Opening));
            MediaPlayer.Playing += (sender, e) => WeakReferenceMessenger.Default.Send(new CameraStateMessage(CameraState.Playing));
            MediaPlayer.Paused += (sender, e) => WeakReferenceMessenger.Default.Send(new CameraStateMessage(CameraState.Paused));
            MediaPlayer.Stopped += (sender, e) => WeakReferenceMessenger.Default.Send(new CameraStateMessage(CameraState.Stopped));
        }

        public async Task Play(Camera camera)
        {
            Guard.IsNotNull(camera);

            List<string> parametersList = [];

            if (camera.APIType == APIType.LibCamera)
            {
                // with libcamera we need first to create video stream
                List<string> controls = new RasPiCameraDetect().GetCommandLineParameters(camera);
                await AppService.StartRaspberryPIStream(rpiPort, controls);
            }
            else if (camera.APIType == APIType.V4l2)
            {
                parametersList = new V4L2CameraDetect().GetCommandLineParameters(camera);
            }
            else if (camera.APIType == APIType.Dshow)
            {
                parametersList = new DShowCameraDetect().GetCommandLineParameters(camera);
            }

            if (!string.IsNullOrWhiteSpace(FullAddress))
            {
                string[] mediaAdditionalOptions = [];

                using var media = new Media(
                    libVLC,
                    FullAddress,
                    FromType.FromLocation,
                    mediaAdditionalOptions
                    );

                MediaPlayer.SetAdjustFloat(VideoAdjustOption.Enable, 1);

                foreach (string parameter in parametersList)
                {
                    media.AddOption(parameter);
                }

                bool result = MediaPlayer.Play(media);

                if (result)
                {
                    logger.Info($"Playing web camera stream: '{media.Mrl}'");
                }
                else
                {
                    logger.Info($"Failed to play web camera stream: '{media.Mrl}'");
                }
            }
        }

        private string GetFullUrlFromParts(Camera? camera)
        {
            Guard.IsNotNull(camera);
            
            protocol = string.Empty;
            pathAndQuery = string.Empty;
            port = string.Empty;
            address = string.Empty;

            if (camera.APIType == APIType.Dshow)
            {
                protocol = "dshow";
            }
            else if (camera.APIType == APIType.QTCapture)
            {
                protocol = "qtcapture";
                address = camera.Path;
            }
            else if (camera.APIType == APIType.V4l2)
            {
                protocol = "v4l2";
                address = camera.Path;
            }
            else if (camera.APIType == APIType.LibCamera)
            {
                protocol = "tcp/h264";
                address = "localhost";
                port = rpiPort;
            }

            string newRemoteAddress = address;
            string addr = newRemoteAddress;
            string pth = string.IsNullOrWhiteSpace(pathAndQuery) ? "" : pathAndQuery;
            string prt = string.IsNullOrWhiteSpace(port) ? "" : $":{port}";

            if (!string.IsNullOrWhiteSpace(protocol))
            {
                protocol += "://";
            }

            return $"{protocol}{addr}{prt}{pth}";
        }

        public string DefaultAddress(Camera camera)
        {
            Guard.IsNotNull(camera);
            
            FullAddress = GetFullUrlFromParts(camera);
            return FullAddress;
        }
    }
}
