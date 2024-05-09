using CollimationCircles.Messages;
using CollimationCircles.Models;
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
        public ICamera Camera { get; set; } = new Camera();

        public LibVLCService()
        {
            FullAddress = GetFullUrlFromParts();

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

        public async Task Play()
        {
            List<string> parametersList = [];

            if (Camera.APIType == APIType.LibCamera)
            {
                // with libcamera we need first to create video stream
                List<string> controls = new RasPiCameraDetect().GetCommandLineParameters(Camera);
                await AppService.StartRaspberryPIStream(rpiPort, controls);
            }
            else if (Camera.APIType == APIType.V4l2)
            {
                parametersList = new V4L2CameraDetect().GetCommandLineParameters(Camera);
            }
            else if (Camera.APIType == APIType.Dshow)
            {
                parametersList = new DShowCameraDetect().GetCommandLineParameters(Camera);
            }

            if (!string.IsNullOrWhiteSpace(FullAddress))
            {
                string[] mediaAdditionalOptions =
                [
                    //$"--osd",
                    //$"--video-title=my title",
                    //$"--avcodec-hw=any",
                    //$"--zoom=0.25"
                ];

                string parametersString = string.Empty;

                if (parametersList.Count > 0)
                {
                    parametersString = ":" + string.Join(":", parametersList);
                }

                using var media = new Media(
                        libVLC,
                        FullAddress + parametersString,
                        FromType.FromLocation,
                        mediaAdditionalOptions
                        );

                MediaPlayer.Play(media);
                logger.Info($"Playing web camera stream: '{media.Mrl}'");
            }
        }

        private string GetFullUrlFromParts()
        {
            protocol = string.Empty;
            pathAndQuery = string.Empty;
            port = string.Empty;
            address = string.Empty;

            if (Camera.APIType == APIType.Dshow)
            {
                protocol = "dshow";
                address = Camera.Path;
            }
            else if (Camera.APIType == APIType.QTCapture)
            {
                protocol = "qtcapture";
                address = Camera.Path;
            }
            else if (Camera.APIType == APIType.V4l2)
            {
                protocol = "v4l2";
                address = Camera.Path;
            }
            else if (Camera.APIType == APIType.LibCamera)
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

        public string DefaultAddress(ICamera camera)
        {
            Camera = camera;
            FullAddress = GetFullUrlFromParts();
            return FullAddress;
        }
    }
}
