using System.Collections.Generic;

namespace CollimationCircles.Models
{
    public class Camera
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public APIType APIType { get; set; }
        public string Path { get; set; } = string.Empty;
        public List<CameraControl> Controls { get; set; } = [];
    }
}
