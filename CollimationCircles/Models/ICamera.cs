using System.Collections.Generic;

namespace CollimationCircles.Models
{
    public interface ICamera
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public List<ICameraControl> Controls { get; set; }
        public bool IsPlaying { get; set; }
        public int SensorWidth { get; set; }
        public int SensorHeight { get; set; }
        public APIType APIType { get; set; }
    }
}
