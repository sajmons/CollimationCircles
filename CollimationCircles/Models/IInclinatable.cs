﻿namespace CollimationCircles.Models
{
    public interface IInclinatable
    {
        public bool IsInclinatable { get; set; }
        public double InclinationAngle { get; set; }
    }
}
