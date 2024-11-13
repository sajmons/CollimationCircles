using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;

namespace CollimationCirclesFeatures
{
    public record Feature
    {
        public required string Name { get; set; }
        public int? Numeric { get; set; }
        public bool Enabled { get; set; } = false;        
    }
}
