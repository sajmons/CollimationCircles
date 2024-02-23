using LibVLCSharp.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    public interface ILibVLCService
    {
        public string FullAddress { get; set; }
        public MediaPlayer MediaPlayer { get; }
        public StreamSource StreamSource { get; set; }
        public void Play(List<string> controlsArgs);
        public string DefaultAddress(StreamSource streamSource);
    }
}
