using Avalonia.Media;
using Avalonia.Media.Imaging;
using CollimationCircles.Messages;
using CollimationCircles.Services;
using CommunityToolkit.Diagnostics;
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

        private readonly SettingsViewModel settingsViewModel;
        private readonly ICameraService cameraService;
        private readonly OpticalAxisService opticalAxisService = new();

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
            this.settingsViewModel = Ioc.Default.GetRequiredService<SettingsViewModel>();
            this.cameraService = Ioc.Default.GetRequiredService<ICameraService>();

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

        async partial void OnIsLiveAnalysisActiveChanged(bool value)
        {
            //await CheckFeatureLicensed(FeatureList.LiveAnalysis, () =>
            //{
                HandleModeSwitch(value || IsLiveRmseActive);
            //});
        }

        partial void OnIsLiveRmseActiveChanged(bool value)
        {
            HandleModeSwitch(value || IsLiveAnalysisActive);
        }

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
                    liveAnalysisCts = new CancellationTokenSource();
                    _ = Task.Run(() => LiveAnalysisLoop(liveAnalysisCts.Token));
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
                    // FIXME: This is a placeholder for the actual camera snapshot logic. Replace it with the appropriate method to capture an image from the camera.
                    byte[]? data = await cameraService.TakeSnapshotImageAsync();
                    if (data != null)
                    {
                        using var image = Cv2.ImDecode(data, ImreadModes.Color);

                        string description = string.Empty;
                        Mat? advancedImage = null;
                        Mat? rmseImage = null;

                        if (IsLiveAnalysisActive)
                        {
                            var advanced = RunAdvancedAnalysis(image);
                            description += advanced.Description;
                            advancedImage = advanced.Image;

                            // Calculate Screw Instructions
                            var screwVM = settingsViewModel.Items.OfType<ScrewViewModel>().FirstOrDefault();
                            if (screwVM != null && advanced.Wavefront != null)
                            {
                                var adjustments = OpticalAxisService.CalculateScrewAdjustments(
                                    advanced.Wavefront["ComaMagnitude"],
                                    advanced.Wavefront["ComaAngle"],
                                    screwVM.RotationAngle,
                                    screwVM.Count);

                                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                                {
                                    UpdateScrewInstructions(adjustments, screwVM);
                                });
                            }
                            else
                            {
                                Avalonia.Threading.Dispatcher.UIThread.Post(() => ScrewInstructions.Clear());
                            }
                        }
                        else
                        {
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => ScrewInstructions.Clear());
                        }

                        if (IsLiveRmseActive)
                        {
                            var rmse = RunRmseAnalysis(image);
                            description += (string.IsNullOrEmpty(description) ? "" : "\n") + rmse.Description;
                            rmseImage = rmse.Image;
                        }

                        var resultImage = advancedImage ?? rmseImage;

                        if (resultImage != null)
                        {
                            Cv2.ImEncode(".png", resultImage, out var png);
                            using var stream = new MemoryStream(png);
                            var bitmap = new Bitmap(stream);
                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                            {
                                LiveResultText = description;
                                AnalysisPreview = bitmap;
                            });
                        }
                        else
                        {
                            Avalonia.Threading.Dispatcher.UIThread.Post(() => AnalysisPreview = null);
                        }

                        advancedImage?.Dispose();
                        rmseImage?.Dispose();
                    }
                }
                catch (OperationCanceledException)
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        LiveResultText = string.Empty;
                        AnalysisPreview = null;
                        ScrewInstructions.Clear();
                    });
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error in Live Analysis Loop");
                }

                try { await Task.Delay(200, token); }
                catch (OperationCanceledException) { break; }
            }
        }

        private static (string Description, Dictionary<string, double>? Wavefront, CircularFeature? Circle, Mat Image) RunAdvancedAnalysis(Mat image)
        {
            using Mat gray = new();
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);

            var edges = FindEdgePoints(gray);
            var refinedEdges = OpticalAxisService.RefineEdgesSubpixel(gray, edges);
            var circle = OpticalAxisService.FitCircleRobust(refinedEdges);
            var wavefront = OpticalAxisService.AnalyzeStarWavefront(gray);

            // Classic circles for visualization
            Options classicOptions = new() { DoNormalize = true, DoGaussianBlur = true, DoThreshold = true, DoEdge = true };
            using var copy = image.Clone();
            ImageAnalysisService.ProcessImage(copy, classicOptions);
            var classicCircles = ImageAnalysisService.DetectCircles(copy, 50, image.Width / 2, new DetectionParameters { VoteThresholdFraction = 0.8 });

            Mat drawImage = new(image.Height, image.Width, MatType.CV_8UC4, Scalar.All(0));

            // Draw classic circles (Hough) in yellow
            foreach (var c in classicCircles)
            {
                Cv2.Circle(drawImage, new Point(c.X, c.Y), c.Radius, new Scalar(0, 255, 255, 255), 1, LineTypes.AntiAlias);
            }

            // Draw Robust Sub-pixel Circle in Cyan
            if (circle != null)
            {
                Cv2.Circle(drawImage, new Point((int)Math.Round(circle.Center.X), (int)Math.Round(circle.Center.Y)), (int)Math.Round(circle.Radius), new Scalar(255, 255, 0, 255), 2, LineTypes.AntiAlias);
            }

            // Draw Coma Vector in Lime
            if (wavefront != null && circle != null)
            {
                DrawVector(drawImage, (int)wavefront["GeometricX"], (int)wavefront["GeometricY"],
                    (int)(wavefront["GeometricX"] + wavefront["ComaMagnitude"] * circle.Radius * 5.0 * Math.Cos(wavefront["ComaAngle"] * Math.PI / 180.0)),
                    (int)(wavefront["GeometricY"] + wavefront["ComaMagnitude"] * circle.Radius * 5.0 * Math.Sin(wavefront["ComaAngle"] * Math.PI / 180.0)));
            }

            string desc = DescribeAdvancedResult(wavefront, circle);
            return (desc, wavefront, circle, drawImage);
        }

        private static (string Description, Mat Image) RunRmseAnalysis(Mat image)
        {
            Options options = new() { DoNormalize = true, DoGaussianBlur = true, DoThreshold = true, DoEdge = true };
            using var copy = image.Clone();
            ImageAnalysisService.ProcessImage(copy, options);
            var circles = ImageAnalysisService.DetectCircles(copy, 50, image.Width / 2, new DetectionParameters { VoteThresholdFraction = 0.8 });

            Mat drawnImage = new(image.Height, image.Width, MatType.CV_8UC4, Scalar.All(0));
            var analysis = ImageAnalysisService.AnalyzeResult(drawnImage, circles, options);

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
            Cv2.Circle(img, new Point(x1, y1), 4, lime, -1, LineTypes.AntiAlias); // Dot at geometric center

            // Draw arrowhead
            double angle = Math.Atan2(y2 - y1, x2 - x1);
            double arrowSize = 15;
            int ax1 = (int)(x2 - arrowSize * Math.Cos(angle - Math.PI / 6));
            int ay1 = (int)(y2 - arrowSize * Math.Sin(angle - Math.PI / 6));
            int ax2 = (int)(x2 - arrowSize * Math.Cos(angle + Math.PI / 6));
            int ay2 = (int)(y2 - arrowSize * Math.Sin(angle + Math.PI / 6));

            Cv2.Line(img, new Point(x2, y2), new Point(ax1, ay1), lime, 3, LineTypes.AntiAlias);
            Cv2.Line(img, new Point(x2, y2), new Point(ax2, ay2), lime, 3, LineTypes.AntiAlias);
        }

        private static List<Point2d> FindEdgePoints(Mat gray)
        {
            using Mat edges = new();
            Cv2.Canny(gray, edges, 50, 150);
            List<Point2d> points = [];
            int height = edges.Height;
            int width = edges.Width;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (edges.At<byte>(y, x) > 0) points.Add(new Point2d(x, y));
                }
            }
            return points;
        }

        private static string DescribeAdvancedResult(Dictionary<string, double>? wavefront, OpticalAxisService.CircularFeature? circle)
        {
            Guard.IsNotNull(wavefront, nameof(wavefront));
            Guard.IsNotNull(circle, nameof(circle));

            string message = string.Empty;
            if (circle != null)
            {
                message += $"Sub-pixel Circle: Center({circle.Center.X:F2}, {circle.Center.Y:F2}),\nRad: {circle.Radius:F2}\n";
            }
            if (wavefront != null)
            {
                message += $"Coma Magnitude: {wavefront["ComaMagnitude"]:F4}\n";
                message += $"Coma Angle: {wavefront["ComaAngle"]:F1}°\n";
            }
            return message;
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
