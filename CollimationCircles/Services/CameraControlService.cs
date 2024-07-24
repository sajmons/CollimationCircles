using CollimationCircles.Models;
using CommunityToolkit.Mvvm.DependencyInjection;
using OpenCvSharp;
using System;
using System.Collections.Generic;

namespace CollimationCircles.Services
{
    internal class CameraControlService : ICameraControlService, IDisposable
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly object? vc;

        public CameraControlService()
        {
            if (OperatingSystem.IsWindows())
            {
                vc = new VideoCapture();
            }
        }

        public void Set(ControlType controlName, double value, ICamera camera)
        {
            // set camera control for V4L2
            if (camera.APIType is APIType.V4l2 || camera.APIType is APIType.QTCapture)
            {
                new V4L2CameraDetect().SetControl(camera, controlName, value);
            }
            // set camera control for DirectShow
            else if (camera.APIType is APIType.Dshow)
            {
                if (vc is not null)
                {
                    new DShowCameraDetect((VideoCapture)vc).SetControl(camera, controlName, value);
                }
                else
                {
                    logger.Error("videoCapture is null");
                }
            }
            // set camera control for Raspberry PI Camera
            else if (camera.APIType is APIType.LibCamera)
            {
                new RasPiCameraDetect().SetControl(camera, controlName, value);
            }
        }
        public void Dispose()
        {
            if (OperatingSystem.IsWindows())
            {
                if (vc is not null)
                {
                    ((VideoCapture)vc).Release();
                    ((VideoCapture)vc).Dispose();
                }
            }
        }

        public void Open()
        {
            if (OperatingSystem.IsWindows())
            {
                ILibVLCService lib = Ioc.Default.GetRequiredService<ILibVLCService>();
                (vc as VideoCapture)?.Open(lib.Camera.Index);
            }
        }

        public void Release()
        {
            if (OperatingSystem.IsWindows())
            {
                (vc as VideoCapture)?.Release();
            }
        }

        public List<ICamera> GetCameraList()
        {
            List<ICamera> cameras = [];

            if (OperatingSystem.IsWindows())
            {
                if (vc is not null)
                {
                    var dshowCameras = new DShowCameraDetect((VideoCapture)vc).GetCameras();
                    cameras.AddRange(dshowCameras);
                }
            }
            else
            {
                var raspiCameras = new RasPiCameraDetect().GetCameras();
                cameras.AddRange(raspiCameras);

                var v4l2Cameras = new V4L2CameraDetect().GetCameras();
                cameras.AddRange(v4l2Cameras);
            }

            cameras.Add(new Camera() { APIType = APIType.Remote });

            return cameras;
        }
    }
}
