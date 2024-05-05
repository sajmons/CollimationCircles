using CollimationCircles.Models;
using LibVLCSharp.Shared;

namespace CollimationCircles.Services
{
    public interface ILibVLCService
    {
        public string FullAddress { get; set; }
        public MediaPlayer MediaPlayer { get; }
        public void Play();
        public Camera Camera { get; set; }
        public string DefaultAddress(Camera camera);
    }
}
