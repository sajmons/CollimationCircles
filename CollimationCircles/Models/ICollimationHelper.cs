using System;

namespace CollimationCircles.Models
{
    public interface ICollimationHelper : IRotatable, ISizeable, IEditable
    {
        public Guid Id { get; set; }        
        public string Color { get; set; }
        public string Label { get; set; }
        public int Thickness { get; set; }        
        public double Radius { get; set; }
        public bool IsVisible { get; set; }
    }
}
