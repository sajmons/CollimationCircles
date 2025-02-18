using Avalonia.Media.Imaging;
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
using System.Diagnostics;
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

            ImageAnalysisService.FilterComplited += (s, e) =>
            {
                if (IsDebug)
                {                    
                    MagickImage image = new (e.ImageBytes);
                    ShowResultDialog(e.FilterName, image);
                }
            };

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
                DoNormalize = true,
                DoGaussianBlur = true,
                //DoThreshold = true,
                //DoErode = true,
                //DoDilate = true,
                //DoEdge = true,
                DoCrop = true
            };

            MagickImage original = new (image);

            Stopwatch sw = new();
            sw.Start();
            logger.Info($"Start ProcessImage");
            ImageAnalysisService.ProcessImage(image, options);
            sw.Stop();
            logger.Info($"ProcessImage time: {sw.Elapsed:mm\\:ss\\.ff}");

            sw.Start();
            logger.Info($"Start DetectCircles");
            List<Circle> circles = ImageAnalysisService.DetectCircles(
                image, 50, (int)image.Width / 2,
                ImageAnalysisService.DetectionAccuracy.Maximum);
            sw.Stop();
            logger.Info($"DetectCircles time: {sw.Elapsed:mm\\:ss\\.ff}");

            sw.Start();
            logger.Info($"Start AnalyzeResult");
            AnalysisResult result = ImageAnalysisService.AnalyzeResult(original, circles, options);
            sw.Stop();
            logger.Info($"AnalyzeResult time: {sw.Elapsed:mm\\:ss\\.ff}");

            string windowTitle = ResSvc.TryGetString("StarAiryDiscAnalysisResult");                        
            string resultText = DescribeResult(result);

            ShowResultDialog(windowTitle, original, resultText);
        }

        private void ShowResultDialog(string title, MagickImage image, string resultText = "")
        {
            var dialogViewModel = DialogService.CreateViewModel<ImageViewModel>();

            Stream stream = new MemoryStream(image.ToByteArray());            

            dialogViewModel.ImageToDisplay = Bitmap.DecodeToWidth(stream, (int)image.Width);
            dialogViewModel.ImageDescription = resultText;
            dialogViewModel.Title = title;

            DialogService.Show(null, dialogViewModel);
        }

        private static string DescribeResult(AnalysisResult result)
        {
            string message = $"Number of circles detected: {result.CircleCount}\n" +
                $"Center RMSE: {result.CenterRMSE:F2}\n" +
                $"Lower RMSE means better collimation.";            

            return message;
        }        
    }
}
