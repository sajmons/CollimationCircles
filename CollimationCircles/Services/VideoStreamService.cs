using System;
using System.Collections.Generic;
using System.Threading;

namespace CollimationCircles.Services
{
    public class VideoStreamService : IVideoStreamService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public void CloseVideoStream(bool isUVCCamera)
        {
            // kill video stream running in background

            if (OperatingSystem.IsWindows())
            {
                AppService.ExecuteCommand(
                "taskkill",
                ["/f", "/t", "/im", "vlc.exe"]);
            }
            else
            {
                if (isUVCCamera)
                {
                    AppService.ExecuteCommand(
                    "pkill",
                    ["-9", "cvlc"]);
                }
                else
                {
                    AppService.ExecuteCommand(
                    "pkill",
                    [AppService.LIBCAMERA_VID]);
                }
            }
        }

        public void OpenVideoStream(string uvcDevice, bool isUVCCamera, string address)
        {
            try
            {
                if (isUVCCamera)
                {
                    string? windowsVLCPath = AppService.FindVLC();

                    if (OperatingSystem.IsWindows() && string.IsNullOrWhiteSpace(windowsVLCPath))
                    {
                        logger.Warn($"VLC not found. Please install VLC to use camera video streaming feature.");
                        return;
                    }

                    string command = OperatingSystem.IsWindows() ? $"\"{windowsVLCPath}\"" : "cvlc";

                    List<string> parameters = [uvcDevice,
                        "--sout",
                        $"\"#transcode{{vcodec=wmv2,vb=4096,acodec=none}}:http{{mux=asf,dst={address}}}\"",
                        "-I",
                        "null",
                        "--play-and-exit"
                    ];

                    AppService.ExecuteCommand(
                    command,
                    parameters, timeout: 0);

                    Thread.Sleep(1000);

                    logger.Info("VLC UVC camera video stream started");
                }
                else
                {
                    if (OperatingSystem.IsWindows())
                    {
                        logger.Warn("Raspberry PI camera is not supported on Windows");
                        return;
                    }

                    string command = AppService.LIBCAMERA_VID;
                    List<string> parameters = [
                        "-t",
                        "0",
                        "--inline",
                        "--nopreview",
                        "--listen",
                        "-o",
                        $"tcp://{address}"];

                    AppService.ExecuteCommand(
                    command,
                    parameters, timeout: 1000);

                    logger.Info("Raspberry PI camera video stream started");
                }
            }
            catch (Exception exc)
            {
                logger.Error(exc);
            }
        }
    }
}