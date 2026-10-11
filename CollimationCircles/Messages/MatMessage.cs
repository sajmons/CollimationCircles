using CommunityToolkit.Mvvm.Messaging.Messages;
using OpenCvSharp;

namespace CollimationCircles.Messages
{
    public class MatMessage(Mat mat) : ValueChangedMessage<Mat>(mat)
    {
    }
}
