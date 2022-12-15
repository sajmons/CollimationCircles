using Avalonia.Media;
using System;

namespace CollimationCircles.Models
{
    public interface ICollimationHelper : IRotatable, ISizeable, IEditable, ICountable, IRotation, ISize
    {
        public Guid Id { get; set; }        
        public Color ItemColor { get; set; }
        public string Label { get; set; }
        public int Thickness { get; set; }        
        public double Radius { get; set; }
        public bool IsVisible { get; set; }
        public int Count { get; set; }
    }
}
