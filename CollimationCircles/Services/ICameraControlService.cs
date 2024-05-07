using CollimationCircles.Models;
using System.Collections.Generic;

namespace CollimationCircles.Services
{
    public interface ICameraControlService
    {
        public void Set(ControlType propertyname, double value, Camera camera);
        public void Open();
        public void Release();
        public List<Camera> GetCameraList();
    }
}
