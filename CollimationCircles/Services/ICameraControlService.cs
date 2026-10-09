using CollimationCircles.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    public interface ICameraControlService
    {
        public void Set(ControlType propertyname, double value, Camera camera);
        public void SetAuto(ControlType propertyname, bool isAuto, Camera camera);

        public bool IsServerConnected { get; }
        public Task<List<Camera>> ConnectServer(string address, int port);
        public Task DisconnectServer();
        public Task StartCamera(Camera camera);
        public Task StopCamera(Camera camera);
    }
}
