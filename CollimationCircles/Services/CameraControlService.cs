using CollimationCircles.Models;
using CollimationCircles.Services.Uvc;
using CollimationCircles.Services.Zwo;
using CommunityToolkit.Diagnostics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    internal class CameraControlService : ICameraControlService
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public void Set(ControlType controlName, double value, Camera camera)
        {
            Guard.IsNotNull(camera);
            Guard.IsTrue(camera.IsPlaying);

            logger.Info($"Dispatching camera control set: camera='{camera.Name}', api={camera.APIType}, control={controlName}, value={value}");

            // UVC camera controls (Linux/Windows/macOS)
            if (camera.APIType is APIType.Uvc)
            {
                if (OperatingSystem.IsMacOS())
                {
                    new UvcCameraDetectMac().SetControl(camera, controlName, value);
                }
                else if (OperatingSystem.IsLinux())
                {
                    new UvcCameraDetectLinux().SetControl(camera, controlName, value);
                }
                else if (OperatingSystem.IsWindows())
                {
                    new UvcCameraDetectWindows().SetControl(camera, controlName, value);
                }
            }

            // ZWO camera controls (Linux/Windows/macOS)
            else if (camera.APIType is APIType.Zwo)
            {
                new ZWOCameraDetect().SetControl(camera, controlName, value);
            }

            // V4L2 camera controls (Linux cameras)
            else if (camera.APIType is APIType.V4l2)
            {
                new V4L2CameraDetect().SetControl(camera, controlName, value);
            }
            
            // macOS system cameras (AVFoundation/QTCapture fallback)
            else if (camera.APIType is APIType.QTCapture)
            {
                new MacOSCameraDetect().SetControl(camera, controlName, value);
            }

            // DirectShow cameras (Windows)
            else if (camera.APIType is APIType.Dshow)
            {
                new DShowCameraDetect().SetControl(camera, controlName, value);
            }

            // Raspberry Pi cameras (Linux)
            else if (camera.APIType is APIType.LibCamera)
            {
                new RasPiCameraDetect().SetControl(camera, controlName, value);
            }
        }

        public void SetAuto(ControlType controlName, bool isAuto, Camera camera)
        {
            Guard.IsNotNull(camera);

            logger.Info($"Dispatching camera auto-control set: camera='{camera.Name}', api={camera.APIType}, control={controlName}, isAuto={isAuto}, isPlaying={camera.IsPlaying}");

            // UVC camera auto-controls (Linux/Windows/macOS)
            if (camera.APIType is APIType.Uvc)
            {
                if (OperatingSystem.IsMacOS())
                {
                    new UvcCameraDetectMac().SetControlAuto(camera, controlName, isAuto);
                }
                else if (OperatingSystem.IsLinux())
                {
                    new UvcCameraDetectLinux().SetControlAuto(camera, controlName, isAuto);
                }
                else if (OperatingSystem.IsWindows())
                {
                    new UvcCameraDetectWindows().SetControlAuto(camera, controlName, isAuto);
                }
            }

            // ZWO camera auto-controls (Linux/Windows/macOS)
            else if (camera.APIType is APIType.Zwo)
            {
                new ZWOCameraDetect().SetControlAuto(camera, controlName, isAuto);
            }
        }

        public async Task<List<Camera>> GetCameraList()
        {
            List<Camera> cameras = [];

            if (OperatingSystem.IsWindows())
            {
                var dshowCameras = await new DShowCameraDetect().GetCameras();
                cameras.AddRange(dshowCameras);

                // Also detect UVC cameras via libuvc on Windows
                var windowsUvcCameras = await new UvcCameraDetectWindows().GetCameras();
                cameras.AddRange(windowsUvcCameras);
            }
            else if (OperatingSystem.IsMacOS())
            {
                var macosCameras = await new MacOSCameraDetect().GetCameras();
                cameras.AddRange(macosCameras);

                // Also detect UVC cameras via libuvc on macOS
                var macosUvcCameras = await new UvcCameraDetectMac().GetCameras();
                cameras.AddRange(macosUvcCameras);

                var raspiCameras = await new RasPiCameraDetect().GetCameras();
                cameras.AddRange(raspiCameras);

                var v4l2Cameras = await new V4L2CameraDetect().GetCameras();
                cameras.AddRange(v4l2Cameras);
            }
            else
            {
                var raspiCameras = await new RasPiCameraDetect().GetCameras();
                cameras.AddRange(raspiCameras);

                var v4l2Cameras = await new V4L2CameraDetect().GetCameras();
                cameras.AddRange(v4l2Cameras);

                // Also detect UVC cameras via libuvc on Linux
                var linuxUvcCameras = await new UvcCameraDetectLinux().GetCameras();
                cameras.AddRange(linuxUvcCameras);
            }

            var zwoCameras = await new ZWOCameraDetect().GetCameras();
            cameras.AddRange(zwoCameras);

            cameras.Add(new Camera() { APIType = APIType.Remote });

            return cameras;
        }
    }
}
