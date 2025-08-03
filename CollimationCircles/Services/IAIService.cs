using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    interface IAIService
    {
        public void AnalyzeImage(string apiKey, string pathToImage);
    }
}
