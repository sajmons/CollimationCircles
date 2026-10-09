using CollimationCircles.Models;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    public interface ICameraService
    {
        public string FullAddress { get; set; }
        public bool IsAvailable { get; }
        public bool IsPlaying { get; }
        public Task Play(Camera camera);
        public Task Stop(Camera camera);
        public string DefaultAddress(Camera camera);
        public Task<byte[]?> TakeSnapshotImageAsync();
    }
}
