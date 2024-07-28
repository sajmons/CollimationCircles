using CollimationCircles.Models;
using System.Collections.Generic;

namespace CollimationCircles.Services
{
    internal interface ICameraDetect
    {
        public Dictionary<ControlType, object> ControlMapping { get; }
        public List<ICamera> GetCameras();        
        public List<string> GetCommandLineParameters(ICamera camera);
    }
}
