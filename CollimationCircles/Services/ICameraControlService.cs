using CollimationCircles.Models;
using System;

namespace CollimationCircles.Services
{
    public interface ICameraControlService
    {
        public void Set(string propertyname, double value, Camera camera);
        public void Open();
        public void Release();
    }
}
