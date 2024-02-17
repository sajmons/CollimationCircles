using CollimationCircles.Helper;
using OpenCvSharp;
using System;
using System.ComponentModel.DataAnnotations;

namespace CollimationCircles.Services
{
    internal class CameraControlService : ICameraControlService, IDisposable
    {
        readonly VideoCapture videoCapture = new();

        [Range(Constraints.BrightnessMin, Constraints.BrightnessMax)]
        public double Brightness
        {
            get => videoCapture.Brightness;
            set => videoCapture.Brightness = value;
        }

        [Range(Constraints.ContrastMin, Constraints.ContrastMax)]
        public double Contrast
        {
            get => videoCapture.Contrast;
            set => videoCapture.Contrast = value;
        }

        [Range(Constraints.SaturationMin, Constraints.SaturationMax)]
        public double Saturation
        {
            get => videoCapture.Saturation;
            set => videoCapture.Saturation = value;
        }

        [Range(Constraints.HueMin, Constraints.HueMax)]
        public double Hue
        {
            get => videoCapture.Hue;
            set => videoCapture.Hue = value;
        }

        [Range(Constraints.GainMin, Constraints.GainMax)]
        public double Gain
        {
            get => videoCapture.Gain;
            set => videoCapture.Gain = value;
        }

        public bool Autofocus
        {
            get => videoCapture.AutoFocus;
            set => videoCapture.AutoFocus = value;
        }

        [Range(Constraints.FocusMin, Constraints.FocusMax)]
        public double Focus
        {
            get => videoCapture.Focus;
            set => videoCapture.Focus = value;
        }        

        [Range(Constraints.BrightnessMin, Constraints.BrightnessMax)]
        public bool Monochrome
        {
            get => videoCapture.Monocrome == 1;
            set => videoCapture.Monocrome = value ? 1.0 : 0.0;
        }

        [Range(Constraints.GammaMin, Constraints.GammaMax)]
        public double Gamma
        {
            get => videoCapture.Gamma;
            set => videoCapture.Gamma = value;
        }

        [Range(Constraints.SharpnessMin, Constraints.SharpnessMax)]
        public double Sharpness
        {
            get => videoCapture.Sharpness;
            set => videoCapture.Sharpness = value;
        }

        [Range(Constraints.ZoomMin, Constraints.ZoomMax)]
        public double Zoom
        {
            get => videoCapture.Zoom;
            set => videoCapture.Zoom = value;
        }        

        public void Dispose()
        {
            videoCapture.Dispose();
        }

        public void Open()
        {
            videoCapture.Open(0);
        }

        public void Release()
        {
            videoCapture.Release();
        }
    }
}
