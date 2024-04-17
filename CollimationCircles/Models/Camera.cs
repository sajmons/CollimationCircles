using System.Collections.Generic;

namespace CollimationCircles.Models
{
    public struct Camera
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public APIType APIType { get; set; }
        public string Path { get; set; }
        public List<CameraControl> Controls { get; set; }

        public Camera()
        {
            Name = string.Empty;
            Path = string.Empty;
            Controls = [];
        }
    }
}
