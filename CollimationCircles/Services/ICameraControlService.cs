using System;

namespace CollimationCircles.Services
{
    public interface ICameraControlService
    {
        public event EventHandler? OnOpened;
        public event EventHandler? OnClosed;

        public int Brightness { get; set; }
        public int Contrast { get; set; }
        public int Saturation { get; set; }
        public int Hue { get; set; }
        public int Gain { get; set; }
        public bool Autofocus { get; set; }
        public int Focus { get; set; }
        public bool Monochrome { get; set; }
        public int Gamma { get; set; }
        public int Sharpness { get; set; }
        public int Zoom { get; set; }        
        public void Open();
        public void Release();
    }
}
