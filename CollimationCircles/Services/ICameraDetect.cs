using CollimationCircles.Models;
using System.Collections.Generic;

namespace CollimationCircles.Services
{
    internal interface ICameraDetect
    {
        public Dictionary<ControlType, object> ControlMapping { get; }
        public List<Camera> GetCameras();
        public List<string> GetCommandLineParameters(Camera camera, ICommandBuilder? builder);
        public void SetControl(Camera camera, ControlType controlName, double value);
        public List<ICameraControl> GetControls(Camera camera);
    }
}
