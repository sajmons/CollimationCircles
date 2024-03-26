using CollimationCircles.Services;

namespace CollimationCircles.Models
{
    internal class CameraControl
    {        
        public string Name { get; set; }
        public int Min { get; set; }
        public int Max { get; set; }
        public int Step { get; set; }
        public int Default { get; set; }
        public int Value { get; set; }
        public string Flags { get; set; }
    }
}
