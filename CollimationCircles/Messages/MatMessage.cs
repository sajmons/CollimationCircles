using CommunityToolkit.Mvvm.Messaging.Messages;
using OpenCvSharp;

namespace CollimationCircles.Messages
{
    public class MatMessage(Mat mat, bool display = true) : ValueChangedMessage<Mat>(mat)
    {
        /// <summary>
        /// Gets whether the frame should be shown in the stream window. Frames that are not displayed
        /// are still available for analysis and only the overlay is drawn.
        /// </summary>
        public bool Display { get; } = display;
    }
}
