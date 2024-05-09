using CollimationCircles.Models;
using System.Collections.Generic;

namespace CollimationCircles.Services
{
    public interface ICameraControlService
    {
        public void Set(ControlType propertyname, double value, ICamera camera);
        public void Open();
        public void Release();
        public List<ICamera> GetCameraList();
    }
}
