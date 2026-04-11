using CollimationCircles.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    public interface ICameraControlService
    {
        public void Set(ControlType propertyname, double value, Camera camera);

        public Task<List<Camera>> GetCameraList();
    }
}
