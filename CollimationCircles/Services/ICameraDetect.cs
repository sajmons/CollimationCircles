using CollimationCircles.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    internal interface ICameraDetect
    {
        public Dictionary<ControlType, object> ControlMapping { get; }
        public Task<List<Camera>> GetCameras();
        public List<string> GetCommandLineParameters(Camera camera, ICommandBuilder? builder);
        public void SetControl(Camera camera, ControlType controlName, double value);
        public Task<List<ICameraControl>> GetControls(Camera camera);
    }
}
