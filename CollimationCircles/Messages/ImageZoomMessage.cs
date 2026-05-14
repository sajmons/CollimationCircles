using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CollimationCircles.Messages
{
    public enum ImageZoomAction
    {
        In,
        Out,
        Reset
    }

    public class ImageZoomMessage(ImageZoomAction action) : ValueChangedMessage<ImageZoomAction>(action)
    {
    }
}
