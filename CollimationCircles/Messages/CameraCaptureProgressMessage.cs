using CommunityToolkit.Mvvm.Messaging.Messages;

namespace CollimationCircles.Messages
{
    public enum CameraCaptureStage
    {
        Idle,
        Exposing,
        Downloading,
        Processing
    }

    public record CameraCaptureProgress(CameraCaptureStage Stage, double Progress, string Status, bool IsActive);

    public class CameraCaptureProgressMessage(CameraCaptureProgress progress) : ValueChangedMessage<CameraCaptureProgress>(progress)
    {
    }
}
