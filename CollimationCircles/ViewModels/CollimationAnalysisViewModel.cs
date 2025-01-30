using CollimationCircles.Messages;
using CollimationCircles.Services;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FileSystem;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using OpenCvSharp;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using static CollimationCircles.Services.ImageAnalysisService;

namespace CollimationCircles.ViewModels
{
    public partial class CollimationAnalysisViewModel : BaseViewModel
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly ILibVLCService libVLCService;

        private string lastPath = ".\\";

        private Mat lastImage = new();

        [ObservableProperty]
        bool isPlaying = false;

        [ObservableProperty]
        bool isCircleHoughTransform = true;

        [ObservableProperty]
        bool isContourMinimumEnclosingCircle = false;

        [ObservableProperty]
        bool isDebug = false;

        [ObservableProperty]
        bool isImageLoaded = false;

        bool loadedFromFile = false;

        public CollimationAnalysisViewModel(ILibVLCService libVLCService)
        {
            this.libVLCService = libVLCService;

            WeakReferenceMessenger.Default.Register<CameraStateMessage>(this, (r, m) =>
            {
                switch (m.Value)
                {
                    case CameraState.Stopped:
                        IsPlaying = false;
                        break;
                    case CameraState.Playing:
                        IsPlaying = true;
                        break;
                }
            });
        }

        [RelayCommand]
        private void TakeSnapshot()
        {
            Guard.IsNotNull(libVLCService);

            if (!IsImageLoaded)
            {
                DialogService.ShowMessageBoxAsync(null,
                    ResSvc.TryGetString("NoVideoStreamWarning"),
                    ResSvc.TryGetString("NoVideoStreamMsgBoxTitle"),
                    MessageBoxButton.Ok);
            }
            else
            {
                libVLCService.TakeSnapshot();

                loadedFromFile = false;

                lastImage = ReLoadImage();

                ProcessAndAnalyze(lastImage);
            }
        }

        [RelayCommand]
        public async Task LoadImage()
        {
            var settings = new OpenFileDialogSettings
            {
                SuggestedFileName = "*.jpg",
                SuggestedStartLocation = new DesktopDialogStorageFolder(new DirectoryInfo(lastPath)),
                Title = ResSvc.TryGetString("OpenFile"),
                Filters =
                [
                    new("JPEG", "jpg")
                ]
            };

            var path = await DialogService.ShowOpenFileDialogAsync(null, settings);

            if (!string.IsNullOrWhiteSpace(path?.Path?.LocalPath))
            {
                lastPath = $"{path?.Path?.LocalPath}";

                loadedFromFile = true;

                lastImage = ReLoadImage();

                ProcessAndAnalyze(lastImage);
            }
        }

        private Mat ReLoadImage()
        {
            Mat img;

            if (loadedFromFile)
            {
                img = ImageAnalysisService.LoadImage(lastPath, ImreadModes.Color);
                IsImageLoaded = true;
            }
            else
            {
                img = ImageAnalysisService.LoadImage($"{LibVLCService.SnapshotImageFile}", ImreadModes.Color);
                IsImageLoaded = true;
            }

            return img;
        }

        private void ProcessAndAnalyze(Mat image)
        {
            AnalysisType analysisType = AnalysisType.CircleHoughTransform;

            if (IsContourMinimumEnclosingCircle)
            {
                analysisType = AnalysisType.ContourMinimumEnclosingCircle;
            }

            Options options = new()
            {
                ShowEachImage = IsDebug,
                DoNormalize = true,
                DoGaussianBlur = true,
                DoAdaptiveThreshold = true,
                DoMorphologyEx = true,
                DoCanny = true
            };

            Mat processed = ImageAnalysisService.ProcessImage(image, options);

            List<CircleSegment> circles;
            AnalysisResult result = new();

            switch (analysisType)
            {
                case AnalysisType.CircleHoughTransform:
                    circles = ImageAnalysisService.DetectHoughCircles(processed, options);
                    result = ImageAnalysisService.AnalyzeResult(image, circles, options);
                    break;
                case AnalysisType.ContourMinimumEnclosingCircle:
                    circles = ImageAnalysisService.DetectCirclesFromContour(processed, options);
                    result = ImageAnalysisService.AnalyzeResult(image, circles, options);
                    break;
            }

            try
            {
                Cv2.DestroyAllWindows();
            }
            catch
            {
            }

            // Display the result
            Cv2.ImShow(ResSvc.TryGetString("StarAiryDiscAnalysisResult"), image);
            DescribeResult(image, result, options);
        }

        private void DescribeResult(Mat image, AnalysisResult result, Options options)
        {
            string message = string.Empty;

            if (result?.Offset == -1)
            {
                message = $"Unable to detect defocused star.\nPlease point your telescope to bright star and then defocuse it.";
            }
            else
            {
                if (result?.CircleCount < options.MinCirclesDetected)
                {
                    message += $"\nNumber of detected circles is to small.";
                }
                else if (result?.Offset < options.OffsetLimit)
                {
                    message += $"Offset from optical axis: {result.Offset:F3}px\nTelescope is likely well-collimated.";
                }
                else
                {
                    message += "\nCollimation issues detected.";
                }
            }

            DrawTextOnImage(message, image);
        }

        public static void DrawTextOnImage(string text, Mat img, int x0 = 10, int y0 = 15, int dy = 20,
            HersheyFonts font = HersheyFonts.HersheyPlain, double fontScale = 0.8, int fontThickness = 1)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            // Split text into lines
            string[] lines = text.Split('\n');

            // Iterate through lines and draw text
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] is null || string.IsNullOrWhiteSpace(lines[i])) continue;

                int y = y0 + i * dy;
                Cv2.PutText(img, lines[i], new Point(x0, y), font, fontScale, Scalar.Yellow, fontThickness);
            }
        }

        partial void OnIsCircleHoughTransformChanged(bool value)
        {
            if (!isImageLoaded || !value) return;

            lastImage = ReLoadImage();

            ProcessAndAnalyze(lastImage);
        }

        partial void OnIsContourMinimumEnclosingCircleChanged(bool value)
        {
            if (!isImageLoaded || !value) return;

            lastImage = ReLoadImage();

            ProcessAndAnalyze(lastImage);
        }
    }
}
