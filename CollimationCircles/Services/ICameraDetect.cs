using CollimationCircles.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    internal interface ICameraDetect
    {
        public bool IsConnected { get; }
        public Task<List<Camera>> Connect(string address, int port);
        public Task Disconnect();
        public Task Start(Camera camera);
        public Task Stop(Camera camera);
        public void SetControl(Camera camera, ControlType controlName, double value);
    }
}
