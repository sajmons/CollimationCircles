using CommunityToolkit.Mvvm.ComponentModel;
using Newtonsoft.Json;

namespace CollimationCircles.Models
{
    public interface IInclinatable
    {
        public bool IsInclinatable { get; set; }
        public double InclinationAngle { get; set; }
    }
}
