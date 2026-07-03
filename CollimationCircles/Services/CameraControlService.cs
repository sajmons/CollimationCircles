using CollimationCircles.Models;
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

            // set camera control for V4L2 (Linux cameras)
            if (camera.APIType is APIType.V4l2)
            {
                new V4L2CameraDetect().SetControl(camera, controlName, value);
            }
            // set camera control for ZWO astro cameras (Windows/macOS)
            else if (camera.APIType is APIType.Zwo)
            {
                new ZWOCameraDetect().SetControl(camera, controlName, value);
            }
            // set camera control for macOS UVC cameras (IOKit + libusb)
            else if (camera.APIType is APIType.Uvc)
            {
                new MacOSCameraDetect().SetControl(camera, controlName, value);
            }
            // set camera control for macOS system cameras (AVFoundation/QTCapture fallback)
            else if (camera.APIType is APIType.QTCapture)
            {
                new MacOSCameraDetect().SetControl(camera, controlName, value);
            }
            // set camera control for DirectShow (Windows)
            else if (camera.APIType is APIType.Dshow)
            {
                new DShowCameraDetect().SetControl(camera, controlName, value);
            }
            // set camera control for Raspberry PI Camera
            else if (camera.APIType is APIType.LibCamera)
            {
                new RasPiCameraDetect().SetControl(camera, controlName, value);
            }
        }

        public void SetAuto(ControlType controlName, bool isAuto, Camera camera)
        {
            Guard.IsNotNull(camera);

            logger.Info($"Dispatching camera auto-control set: camera='{camera.Name}', api={camera.APIType}, control={controlName}, isAuto={isAuto}, isPlaying={camera.IsPlaying}");

            if (camera.APIType is APIType.Zwo)
            {
                new ZWOCameraDetect().SetControlAuto(camera, controlName, isAuto);
            }
            else if (camera.APIType is APIType.Uvc)
            {
                new MacOSCameraDetect().SetControlAuto(camera, controlName, isAuto);
            }
        }

        public async Task<List<Camera>> GetCameraList()
        {
            List<Camera> cameras = [];

            if (OperatingSystem.IsWindows())
            {
                var dshowCameras = await new DShowCameraDetect().GetCameras();
                cameras.AddRange(dshowCameras);
            }
            else if (OperatingSystem.IsMacOS())
            {
                var macosCameras = await new MacOSCameraDetect().GetCameras();
                cameras.AddRange(macosCameras);

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
            }

            var zwoCameras = await new ZWOCameraDetect().GetCameras();
            cameras.AddRange(zwoCameras);

            cameras.Add(new Camera() { APIType = APIType.Remote });

            return cameras;
        }
    }
}
