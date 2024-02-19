using System;

namespace CollimationCircles.Services
{
    public interface ICameraControlService
    {
        public event EventHandler? OnOpened;
        public event EventHandler? OnClosed;

        // user controls
        public int Brightness { get; set; }
        public int Contrast { get; set; }
        public int Saturation { get; set; }
        public int Hue { get; set; }
        public int Gamma { get; set; }
        public bool AutoWhiteBalance { get; set; }
        public int Temperature { get; set; }
        public int Gain { get; set; }
        public int Sharpness { get; set; }
        public bool Autofocus { get; set; }
        public int Focus { get; set; }

        // camera controls
        public bool AutoExposure { get; set; }
        public int ExposureTime { get; set; }
        public int Zoom { get; set; }
        public void Open();
        public void Release();
    }
}
