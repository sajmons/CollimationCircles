using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    public interface ICameraControlService
    {
        public double Brightness { get; set; }
        public double Contrast { get; set; }
        public double Saturation { get; set; }
        public double Hue { get; set; }
        public double Gain { get; set; }
        public bool Autofocus { get; set; }
        public double Focus { get; set; }
        public bool Monochrome { get; set; }
        public double Gamma { get; set; }
        public double Sharpness { get; set; }
        public double Zoom { get; set; }
        public void Open();
        public void Release();
    }
}
