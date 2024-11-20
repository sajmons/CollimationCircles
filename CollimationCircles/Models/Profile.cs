using System.Collections.Generic;

namespace CollimationCircles.Models
{
    public class Profile
    {
        public required string Name { get; set; }
        public required List<CollimationHelper> ScopeShapes { get; set; }
    }
}
