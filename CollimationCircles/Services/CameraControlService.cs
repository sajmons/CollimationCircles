using CollimationCircles.Helper;
using OpenCvSharp;
using System;
using System.ComponentModel.DataAnnotations;

namespace CollimationCircles.Services
{
    internal class CameraControlService : ICameraControlService, IDisposable
    {
        readonly VideoCapture videoCapture = new();

        public event EventHandler? OnOpened;
        public event EventHandler? OnClosed;

        [Range(Constraints.BrightnessMin, Constraints.BrightnessMax)]
        public int Brightness
        {
            get => (int)videoCapture.Brightness;
            set => videoCapture.Brightness = value;
        }

        [Range(Constraints.ContrastMin, Constraints.ContrastMax)]
        public int Contrast
        {
            get => (int)videoCapture.Contrast;
            set => videoCapture.Contrast = value;
        }

        [Range(Constraints.SaturationMin, Constraints.SaturationMax)]
        public int Saturation
        {
            get => (int)videoCapture.Saturation;
            set => videoCapture.Saturation = value;
        }

        [Range(Constraints.HueMin, Constraints.HueMax)]
        public int Hue
        {
            get => (int)videoCapture.Hue;
            set => videoCapture.Hue = value;
        }

        [Range(Constraints.GainMin, Constraints.GainMax)]
        public int Gain
        {
            get => (int)videoCapture.Gain;
            set => videoCapture.Gain = value;
        }

        public bool Autofocus
        {
            get => videoCapture.AutoFocus;
            set => videoCapture.AutoFocus = value;
        }

        [Range(Constraints.FocusMin, Constraints.FocusMax)]
        public int Focus
        {
            get => (int)videoCapture.Focus;
            set => videoCapture.Focus = value;
        }        

        [Range(Constraints.GammaMin, Constraints.GammaMax)]
        public int Gamma
        {
            get => (int)videoCapture.Gamma;
            set => videoCapture.Gamma = value;
        }

        public bool AutoWhiteBalance
        {
            get => (int)videoCapture.XI_AutoWB == 1;
            set => videoCapture.XI_AutoWB = value ? 1.0 : 0.0;
        }

        [Range(Constraints.TemperatureMin, Constraints.TemperatureMax)]
        public int Temperature
        {
            get => (int)videoCapture.Temperature;
            set => videoCapture.Temperature = value;
        }

        [Range(Constraints.SharpnessMin, Constraints.SharpnessMax)]
        public int Sharpness
        {
            get => (int)videoCapture.Sharpness;
            set => videoCapture.Sharpness = value;
        }

        [Range(Constraints.ZoomMin, Constraints.ZoomMax)]
        public int Zoom
        {
            get => (int)videoCapture.Zoom;
            set => videoCapture.Zoom = value;
        }

        public bool AutoExposure
        {
            get => videoCapture.AutoExposure == 1;
            set => videoCapture.AutoExposure = value ? 1.0 : 0.0;
        }

        [Range(Constraints.ExposureTimeMin, Constraints.ExposureTimeMax)]
        public int ExposureTime
        {
            get => (int)videoCapture.Exposure;
            set => videoCapture.Exposure = value;
        }        

        public void Dispose()
        {
            videoCapture.Dispose();
        }

        public void Open()
        {
            videoCapture.Open(0);
            OnOpened?.Invoke(this, new EventArgs());
        }

        public void Release()
        {
            videoCapture.Release();
            OnClosed?.Invoke(this, new EventArgs());
        }
    }
}
