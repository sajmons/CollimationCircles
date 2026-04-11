using Avalonia.Media;

namespace CollimationCircles.Models
{
    public interface ICollimationHelper : IRotatable, IInclinatable, ISizeable, IEditable, ICountable, IRotation, ISize
    {
        public string Id { get; set; }
        public Color ItemColor { get; set; }
        public string Label { get; set; }
        public int Thickness { get; set; }
        public double Radius { get; set; }
        public bool IsVisible { get; set; }
        public int Count { get; set; }
        public bool IsLabelVisible { get; set; }
    }
}
