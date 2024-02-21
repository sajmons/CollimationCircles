using System;

namespace CollimationCircles.Services
{
    public interface ICameraControlService
    {
        public void Set(string propertyname, double value);
        public void Open();
        public void Release();
    }
}
