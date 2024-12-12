using CollimationCircles.Models;
using System.Collections.Generic;

namespace CollimationCircles.Services
{
    public interface ICameraControlService
    {
        public void Set(ControlType propertyname, double value, Camera camera);

        public List<Camera> GetCameraList();
    }
}
