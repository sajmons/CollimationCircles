using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CollimationCircles.Messages
{
    /// <summary>
    /// 8-bit grayscale frame, row-major, Width * Height bytes.
    /// </summary>
    public record CameraFrame(int Width, int Height, byte[] Pixels);

    public class CameraFrameMessage(CameraFrame frame) : ValueChangedMessage<CameraFrame>(frame)
    {
    }
}
