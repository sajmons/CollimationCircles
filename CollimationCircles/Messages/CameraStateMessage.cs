using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CollimationCircles.Messages
{
    public class CameraStateMessage(bool isOpened) : ValueChangedMessage<bool>(isOpened)
    {
    }
}
