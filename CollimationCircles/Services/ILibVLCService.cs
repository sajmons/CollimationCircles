using CollimationCircles.Models;
using LibVLCSharp.Shared;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    public interface ILibVLCService
    {
        public string FullAddress { get; set; }
        public MediaPlayer MediaPlayer { get; }
        public Task Play(Camera camera);
        public string DefaultAddress(Camera camera);
    }
}
