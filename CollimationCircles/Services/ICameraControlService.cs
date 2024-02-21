using System;

namespace CollimationCircles.Services
{
    public interface ICameraControlService
    {
        public event EventHandler? OnOpened;
        public event EventHandler? OnReleased;
        public void Set(string propertyname, double value);
        public void Open();
        public void Release();
    }
}
