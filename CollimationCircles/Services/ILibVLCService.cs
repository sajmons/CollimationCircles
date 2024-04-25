using CollimationCircles.Models;
using LibVLCSharp.Shared;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    public interface ILibVLCService
    {
        public string FullAddress { get; set; }
        public MediaPlayer MediaPlayer { get; }
        public void Play(List<string> controlsArgs);
        public Camera Camera { get; set; }
        public string DefaultAddress(Camera camera);
    }
}
