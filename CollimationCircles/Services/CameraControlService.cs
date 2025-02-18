using CollimationCircles.Models;
using CommunityToolkit.Diagnostics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    internal class CameraControlService : ICameraControlService
    {
        public void Set(ControlType controlName, double value, Camera camera)
        {
            Guard.IsNotNull(camera);

            // set camera control for V4L2
            if (camera.APIType is APIType.V4l2 || camera.APIType is APIType.QTCapture)
            {
                new V4L2CameraDetect().SetControl(camera, controlName, value);
            }
            // set camera control for DirectShow
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

        public async Task<List<Camera>> GetCameraList()
        {
            List<Camera> cameras = [];

            if (OperatingSystem.IsWindows())
            {
                var dshowCameras = await new DShowCameraDetect().GetCameras();
                cameras.AddRange(dshowCameras);
            }
            else
            {
                var raspiCameras = await new RasPiCameraDetect().GetCameras();
                cameras.AddRange(raspiCameras);

                var v4l2Cameras = await new V4L2CameraDetect().GetCameras();
                cameras.AddRange(v4l2Cameras);
            }

            cameras.Add(new Camera() { APIType = APIType.Remote });

            return cameras;
        }
    }
}
