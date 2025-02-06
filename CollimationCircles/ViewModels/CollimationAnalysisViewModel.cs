using CollimationCircles.Messages;
using CollimationCircles.Services;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FileSystem;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
using ImageMagick;
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

        private MagickImage lastImage = new();

        [ObservableProperty]
        bool isPlaying = false;

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
                SuggestedStartLocation = new DesktopDialogStorageFolder(new DirectoryInfo(lastPath)),
                Title = ResSvc.TryGetString("OpenFile"),
                Filters =
                [
                    new("JPEG", [".jpg",".jpeg"])
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

        private MagickImage ReLoadImage()
        {
            MagickImage img;

            if (loadedFromFile)
            {
                img = ImageAnalysisService.LoadImage(lastPath);
                IsImageLoaded = true;
            }
            else
            {
                img = ImageAnalysisService.LoadImage($"{LibVLCService.SnapshotImageFile}");
                IsImageLoaded = true;
            }

            return img;
        }

        private void ProcessAndAnalyze(MagickImage image)
        {
            Options options = new()
            {
                ShowEachImage = IsDebug,
                SaveImages = IsDebug,
                DoNormalize = true,
                DoGaussianBlur = true,
                DoThreshold = true,
                DoErode = true,
                DoDilate = true,
                DoEdge = true
            };

            ImageAnalysisService.ProcessImage(image, options);

            List<Circle> circles = [];

            circles = ImageAnalysisService.DetectCircles(image, 30, 1000, 255, 0.3, 10, 5, 2);

            AnalysisResult result = ImageAnalysisService.AnalyzeResult(image, circles, options);

            string windowTitle = ResSvc.TryGetString("StarAiryDiscAnalysisResult");
                        
            DescribeResult(image, result, options);            
        }

        private void DescribeResult(MagickImage image, AnalysisResult result, Options options)
        {
            string message = $"Number of circles detected: {result.CircleCount}\n";

            if (result?.Offset == -1)
            {
                message += $"Unable to detect defocused star.\nPlease point your telescope to bright star and then defocus it.";
            }
            else
            {
                if (result?.CircleCount < options.MinCirclesDetected)
                {
                    message += $"Number of detected circles is to small.";
                }
                else if (result?.Offset <= options.OffsetLimit)
                {
                    message += $"Offset from optical axis: {result.Offset:F3}px\nTelescope is likely well-collimated.";
                }
                else
                {
                    message += "Collimation issues detected.";
                }
            }

            DrawTextOnImage(image, message);
        }

        public static void DrawTextOnImage(MagickImage img, string text, int x0 = 10, int y0 = 15, int dy = 20)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            // Split text into lines
            string[] lines = text.Split('\n');

            // Iterate through lines and draw text
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] is null || string.IsNullOrWhiteSpace(lines[i])) continue;
                int y = y0 + i * dy;                

                ImageAnalysisService.DrawText(img, lines[i], x0, y, MagickColors.Yellow);
            }
        }        
    }
}
