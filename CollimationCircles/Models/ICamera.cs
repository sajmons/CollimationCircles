using System.Collections.Generic;

namespace CollimationCircles.Models
{
    public interface ICamera
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public APIType APIType { get; set; }
        public string Path { get; set; }
        public List<ICameraControl> Controls { get; set; }
        public void SetDefaultControls();
    }
}
