using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CollimationCircles.Messages
{
    public enum CameraState
    { 
        Opening,
        Playing,
        Paused,
        Stopped
    }

    public class CameraStateMessage(CameraState cameraState) : ValueChangedMessage<CameraState>(cameraState)
    {
    }
}
