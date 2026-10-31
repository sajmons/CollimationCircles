using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CollimationCircles.Messages;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static CollimationCircles.Services.ImageAnalysisService;
using static CollimationCircles.Services.OpticalAxisService;

namespace CollimationCircles.ViewModels
{
    public partial class CollimationAnalysisViewModel : BaseViewModel
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private const int MaxAnalysisWidth = 640;
        private const int AnalysisIntervalMs = 200;
        private const double MinArrowLength = 40;
        private const int MaxDetectedCircles = 8;
        private const double MinCircleRadiusFraction = 50.0 / MaxAnalysisWidth;

        private readonly SettingsViewModel settingsViewModel;
        private readonly ICameraService cameraService;

        [ObservableProperty]
        private ObservableCollection<ScrewInstruction> screwInstructions = [];

        [ObservableProperty]
        bool isStreaming = false;

        [ObservableProperty]
        bool isLiveAnalysisActive = false;

        [ObservableProperty]
        bool isLiveRmseActive = false;

        [ObservableProperty]
        bool isAnyLiveAnalysisActive = false;

        [ObservableProperty]
        string liveResultText = string.Empty;

        [ObservableProperty]
        Bitmap? analysisPreview;

        private CancellationTokenSource? liveAnalysisCts;

        public CollimationAnalysisViewModel()
        {
            settingsViewModel = Ioc.Default.GetRequiredService<SettingsViewModel>();
            cameraService = Ioc.Default.GetRequiredService<ICameraService>();

            WeakReferenceMessenger.Default.Register<CameraStateMessage>(this, (r, m) =>
            {
                switch (m.Value)
                {
                    case CameraState.Stopped:
                        IsStreaming = false;
                        break;
                    case CameraState.Playing:
                        IsStreaming = true;
                        break;
                }
            });
        }

        partial void OnIsLiveAnalysisActiveChanged(bool value) => HandleModeSwitch(value || IsLiveRmseActive);

        partial void OnIsLiveRmseActiveChanged(bool value) => HandleModeSwitch(value || IsLiveAnalysisActive);

        private void HandleModeSwitch(bool anyActive)
        {
            IsAnyLiveAnalysisActive = anyActive;
            if (anyActive)
            {
                if (!IsStreaming)
                {
                    IsLiveAnalysisActive = false;
                    IsLiveRmseActive = false;
                    return;
                }

                if (liveAnalysisCts == null)
                {
                    var cts = liveAnalysisCts = new CancellationTokenSource();
                    _ = Task.Run(() => LiveAnalysisLoop(cts.Token));
                }
            }
            else
            {
                liveAnalysisCts?.Cancel();
                liveAnalysisCts = null;
            }
        }

        private async Task LiveAnalysisLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    byte[]? data = await cameraService.TakeSnapshotImageAsync();
                    if (data != null)
                    {
                        AnalyzeSnapshot(data);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error in Live Analysis Loop");
                }

                try { await Task.Delay(AnalysisIntervalMs, token); }
                catch (OperationCanceledException) { break; }
            }

            Dispatcher.UIThread.Post(() =>
            {
                LiveResultText = string.Empty;
                AnalysisPreview = null;
                ScrewInstructions.Clear();
            });
        }

        private void AnalyzeSnapshot(byte[] data)
        {
            using var fullImage = Cv2.ImDecode(data, ImreadModes.Color);
            if (fullImage.Empty())
            {
                return;
            }

            using var image = DownscaleForAnalysis(fullImage);

            var descriptions = new List<string>();
            Mat? overlay = null;

            try
            {
                if (IsLiveAnalysisActive)
                {
                    var advanced = RunAdvancedAnalysis(image);
                    descriptions.Add(advanced.Description);
                    overlay = advanced.Image;
                    PostScrewInstructions(advanced.Wavefront);
                }
                else
                {
                    Dispatcher.UIThread.Post(ScrewInstructions.Clear);
                }

                if (IsLiveRmseActive)
                {
                    var rmse = RunRmseAnalysis(image);
                    descriptions.Add(rmse.Description);
                    if (overlay is null)
                    {
                        overlay = rmse.Image;
                    }
                    else
                    {
                        rmse.Image.Dispose();
                    }
                }

                Bitmap? bitmap = null;
                if (overlay != null)
                {
                    Cv2.ImEncode(".png", overlay, out var png);
                    using var stream = new MemoryStream(png);
                    bitmap = new Bitmap(stream);
                }

                var text = string.Join("\n", descriptions);
                Dispatcher.UIThread.Post(() =>
                {
                    LiveResultText = text;
                    AnalysisPreview = bitmap;
                });
            }
            finally
            {
                overlay?.Dispose();
            }
        }

        private void PostScrewInstructions(Dictionary<string, double>? wavefront)
        {
            var screwVM = settingsViewModel.Items.OfType<ScrewViewModel>().FirstOrDefault();
            if (screwVM is null || wavefront is null)
            {
                Dispatcher.UIThread.Post(ScrewInstructions.Clear);
                return;
            }

            var adjustments = CalculateScrewAdjustments(
                wavefront["ComaMagnitude"],
                wavefront["ComaAngle"],
                screwVM.RotationAngle,
                screwVM.Count);

            Dispatcher.UIThread.Post(() => UpdateScrewInstructions(adjustments, screwVM));
        }

        private static Mat DownscaleForAnalysis(Mat source)
        {
            if (source.Width <= MaxAnalysisWidth)
            {
                return source.Clone();
            }

            double scale = (double)MaxAnalysisWidth / source.Width;
            var resized = new Mat();
            Cv2.Resize(source, resized, new Size(MaxAnalysisWidth, (int)Math.Round(source.Height * scale)), 0, 0, InterpolationFlags.Area);
            return resized;
        }

        private static Options CreateCircleOptions() => new() { DoNormalize = true, DoGaussianBlur = true, DoThreshold = true, DoEdge = true };

        private static List<Circle> DetectClassicCircles(Mat image, Options options)
        {
            using var gray = new Mat();
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.Normalize(gray, gray, 0, 255, NormTypes.MinMax);
            Cv2.GaussianBlur(gray, gray, new Size(9, 9), 2);

            int minRadius = Math.Max(10, (int)(image.Width * MinCircleRadiusFraction));
            var found = Cv2.HoughCircles(gray, HoughModes.Gradient, 1, minRadius, 100, 30, minRadius, image.Width / 2);

            var circles = found
                .Take(MaxDetectedCircles)
                .Select(c => new Circle { X = (int)Math.Round(c.Center.X), Y = (int)Math.Round(c.Center.Y), Radius = (int)Math.Round(c.Radius), Votes = 0 })
                .ToList();

            logger.Debug($"Hough detection found {circles.Count} circle(s) on {image.Width}x{image.Height} image");
            return circles;
        }

        private static (string Description, Dictionary<string, double>? Wavefront, Mat Image) RunAdvancedAnalysis(Mat image)
        {
            using Mat gray = new();
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.GaussianBlur(gray, gray, new Size(5, 5), 0);
            Cv2.Normalize(gray, gray, 0, 255, NormTypes.MinMax);

            var wavefront = AnalyzeStarWavefront(gray);
            var classicCircles = DetectClassicCircles(image, CreateCircleOptions());

            Mat drawImage = new(image.Height, image.Width, MatType.CV_8UC4, Scalar.All(0));

            foreach (var c in classicCircles)
            {
                Cv2.Circle(drawImage, new Point(c.X, c.Y), c.Radius, new Scalar(0, 255, 255, 255), 1, LineTypes.AntiAlias);
            }

            if (wavefront != null)
            {
                double x = wavefront["GeometricX"];
                double y = wavefront["GeometricY"];
                double radius = wavefront["Radius"];
                var cyan = new Scalar(255, 255, 0, 255);
                Cv2.Circle(drawImage, new Point((int)x, (int)y), (int)Math.Round(radius), cyan, 2, LineTypes.AntiAlias);
                Cv2.Circle(drawImage, new Point((int)wavefront["CentroidX"], (int)wavefront["CentroidY"]), Math.Max(2, (int)Math.Round(wavefront["ShadowRadius"])), new Scalar(0, 0, 255, 255), 2, LineTypes.AntiAlias);
                double length = Math.Clamp(wavefront["ComaMagnitude"] * radius * 20.0, MinArrowLength, Math.Max(MinArrowLength, radius));
                double angle = wavefront["ComaAngle"] * Math.PI / 180.0;
                DrawVector(drawImage, (int)x, (int)y, (int)(x + length * Math.Cos(angle)), (int)(y + length * Math.Sin(angle)));
            }

            return (DescribeAdvancedResult(wavefront), wavefront, drawImage);
        }

        private static (string Description, Mat Image) RunRmseAnalysis(Mat image)
        {
            var options = CreateCircleOptions();
            var circles = DetectClassicCircles(image, options);

            Mat drawnImage = new(image.Height, image.Width, MatType.CV_8UC4, Scalar.All(0));
            var analysis = AnalyzeResult(drawnImage, circles, options);

            string desc = $"--- MECHANICAL RMSE ---\n" +
                         $"Circles: {analysis.CircleCount}\n" +
                         $"RMSE: {analysis.CenterRMSE:F2}\n" +
                         $"------------------------";

            return (desc, drawnImage);
        }

        private static void DrawVector(Mat img, int x1, int y1, int x2, int y2)
        {
            var lime = new Scalar(0, 255, 0, 255);
            Cv2.Line(img, new Point(x1, y1), new Point(x2, y2), lime, 3, LineTypes.AntiAlias);
            Cv2.Circle(img, new Point(x1, y1), 4, lime, -1, LineTypes.AntiAlias);

            double angle = Math.Atan2(y2 - y1, x2 - x1);
            const double arrowSize = 15;
            foreach (var offset in new[] { -Math.PI / 6, Math.PI / 6 })
            {
                var tip = new Point((int)(x2 - arrowSize * Math.Cos(angle + offset)), (int)(y2 - arrowSize * Math.Sin(angle + offset)));
                Cv2.Line(img, new Point(x2, y2), tip, lime, 3, LineTypes.AntiAlias);
            }
        }

        private static string DescribeAdvancedResult(Dictionary<string, double>? wavefront)
        {
            if (wavefront is null)
            {
                return "Star not detected\n";
            }

            return $"Disc radius: {wavefront["Radius"]:F1}px\n" +
                   $"Shadow offset: {wavefront["ComaMagnitude"] * 100:F1}% of radius\n" +
                   $"Offset angle: {wavefront["ComaAngle"]:F1}°\n";
        }

        private void UpdateScrewInstructions(List<ScrewAdjustment> adjustments, ScrewViewModel screwVM)
        {
            if (ScrewInstructions.Count != adjustments.Count)
            {
                ScrewInstructions.Clear();
                foreach (var adj in adjustments)
                {
                    ScrewInstructions.Add(new ScrewInstruction { Id = adj.Id, Color = screwVM.ItemColor });
                }
            }

            for (int i = 0; i < adjustments.Count; i++)
            {
                ScrewInstructions[i].Direction = adjustments[i].Direction;
                ScrewInstructions[i].Turns = adjustments[i].Turns;
            }
        }
    }

    public partial class ScrewInstruction : ObservableObject
    {
        [ObservableProperty]
        private int id;

        [ObservableProperty]
        private string direction = string.Empty;

        [ObservableProperty]
        private double turns;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Brush))]
        private Color color;

        public IBrush Brush => new SolidColorBrush(Color);

        public string DisplayText => Direction == "Fixed" ? "Locked" : $"{Direction} {Turns:F2} turns";
    }
}
