using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CollimationCircles.Messages;
using CollimationCircles.Models;
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
        private const double MinCircleRadiusFraction = 20.0 / MaxAnalysisWidth;
        private const double MinEdgeSupport = 0.6;

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

            settingsViewModel.Items.CollectionChanged += OnItemsChanged;
            foreach (var item in settingsViewModel.Items)
            {
                HookScrewItem(item);
            }

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

        private Dictionary<string, double>? lastScrewInput;

        private static bool IsScrewSetting(string? name) =>
            name is nameof(CollimationHelper.Count) or nameof(CollimationHelper.RotationAngle) or nameof(CollimationHelper.ItemColor);

        private void HookScrewItem(CollimationHelper item)
        {
            if (item is ScrewViewModel)
            {
                item.PropertyChanged -= OnScrewPropertyChanged;
                item.PropertyChanged += OnScrewPropertyChanged;
            }
        }

        private void OnScrewPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (IsScrewSetting(e.PropertyName))
            {
                RefreshScrewInstructions();
            }
        }

        private void OnItemsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            foreach (var item in settingsViewModel.Items)
            {
                HookScrewItem(item);
            }

            RefreshScrewInstructions();
        }

        private void RefreshScrewInstructions()
        {
            if (IsAnyLiveAnalysisActive)
            {
                PostScrewInstructions(lastScrewInput);
            }
        }

        partial void OnIsLiveAnalysisActiveChanged(bool value)
        {
            if (value && IsLiveRmseActive)
            {
                IsLiveRmseActive = false;
            }

            HandleModeSwitch(value || IsLiveRmseActive);
        }

        partial void OnIsLiveRmseActiveChanged(bool value)
        {
            if (value && IsLiveAnalysisActive)
            {
                IsLiveAnalysisActive = false;
            }

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
                else if (!IsLiveRmseActive)
                {
                    Dispatcher.UIThread.Post(ScrewInstructions.Clear);
                }

                if (IsLiveRmseActive)
                {
                    var rmse = RunRmseAnalysis(image);
                    descriptions.Add(rmse.Description);
                    if (!IsLiveAnalysisActive)
                    {
                        PostScrewInstructions(rmse.Offset);
                    }
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
            lastScrewInput = wavefront;
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

        private static double EdgeSupport(Mat edges, double cx, double cy, double radius)
        {
            const int samples = 90;
            int hits = 0;
            for (int i = 0; i < samples; i++)
            {
                double a = 2 * Math.PI * i / samples;
                int x = (int)Math.Round(cx + radius * Math.Cos(a));
                int y = (int)Math.Round(cy + radius * Math.Sin(a));
                if (x >= 0 && y >= 0 && x < edges.Width && y < edges.Height && edges.At<byte>(y, x) > 0)
                {
                    hits++;
                }
            }

            return (double)hits / samples;
        }

        private static List<Circle> DetectClassicCircles(Mat image, Options options)
        {
            using var gray = new Mat();
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.Normalize(gray, gray, 0, 255, NormTypes.MinMax);
            Cv2.GaussianBlur(gray, gray, new Size(7, 7), 2);

            double minRadius = Math.Max(10, image.Width * MinCircleRadiusFraction);
            double maxRadius = image.Width / 2.0;
            var candidates = new List<(Circle Circle, double Area)>();

            // Edge map used to verify that a candidate circle lies on a real image edge.
            using var edges = new Mat();
            Cv2.Canny(gray, edges, 30, 90);
            using var edgeKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(3, 3));
            Cv2.Dilate(edges, edges, edgeKernel);

            // Nested tube edges differ in brightness
            var masks = new List<Mat>();
            for (int level = 25; level <= 225; level += 25)
            {
                var m = new Mat();
                Cv2.Threshold(gray, m, level, 255, ThresholdTypes.Binary);
                var mi = new Mat();
                Cv2.BitwiseNot(m, mi);
                masks.Add(m);
                masks.Add(mi);
            }

            foreach (var mask in masks)
            {
                Cv2.FindContours(mask, out var contours, out _, RetrievalModes.List, ContourApproximationModes.ApproxSimple);
                foreach (var contour in contours)
                {
                    if (contour.Length < 5)
                    {
                        continue;
                    }

                    var box = Cv2.BoundingRect(contour);
                    if (box.Left <= 1 || box.Top <= 1 || box.Right >= image.Width - 1 || box.Bottom >= image.Height - 1)
                    {
                        continue;
                    }

                    var hull = Cv2.ConvexHull(contour);
                    double hullArea = Cv2.ContourArea(hull);
                    if (hullArea < Math.PI * minRadius * minRadius)
                    {
                        continue;
                    }

                    Cv2.MinEnclosingCircle(hull, out var enclosingCenter, out float enclosingRadius);
                    double circleArea = Math.PI * enclosingRadius * enclosingRadius;
                    if (enclosingRadius < minRadius || enclosingRadius > maxRadius || hullArea / circleArea < 0.85)
                    {
                        continue;
                    }

                    var ellipse = Cv2.FitEllipse(hull);
                    double major = Math.Max(ellipse.Size.Width, ellipse.Size.Height);
                    double minor = Math.Min(ellipse.Size.Width, ellipse.Size.Height);
                    if (major <= 0 || minor / major < 0.85)
                    {
                        continue;
                    }

                    double radiusFit = (major + minor) / 4.0;
                    if (EdgeSupport(edges, ellipse.Center.X, ellipse.Center.Y, radiusFit) < MinEdgeSupport)
                    {
                        continue;
                    }

                    candidates.Add((new Circle
                    {
                        X = (int)Math.Round(ellipse.Center.X),
                        Y = (int)Math.Round(ellipse.Center.Y),
                        Radius = (int)Math.Round((major + minor) / 4.0),
                        Votes = 0
                    }, hullArea));
                }
            }

            foreach (var m in masks)
            {
                m.Dispose();
            }

            const int mergeTolerance = 6;
            var circles = new List<Circle>();
            foreach (var (circle, _) in candidates.OrderByDescending(c => c.Area))
            {
                bool duplicate = circles.Any(e =>
                    Math.Abs(e.X - circle.X) <= mergeTolerance && Math.Abs(e.Y - circle.Y) <= mergeTolerance && Math.Abs(e.Radius - circle.Radius) <= mergeTolerance);
                if (!duplicate)
                {
                    circles.Add(circle);
                }

                if (circles.Count >= MaxDetectedCircles)
                {
                    break;
                }
            }

            logger.Debug($"Contour detection found {circles.Count} circle(s) on {image.Width}x{image.Height} image");
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

        private static (string Description, Mat Image, Dictionary<string, double>? Offset) RunRmseAnalysis(Mat image)
        {
            var options = CreateCircleOptions();
            var circles = DetectClassicCircles(image, options);

            Mat drawnImage = new(image.Height, image.Width, MatType.CV_8UC4, Scalar.All(0));
            var analysis = AnalyzeResult(drawnImage, circles, options);

            string desc = $"--- MECHANICAL RMSE ---\n" +
                         $"Circles: {analysis.CircleCount}\n" +
                         $"RMSE: {analysis.CenterRMSE:F2} px\n" +
                         $"RMSE: {analysis.CenterRMSEPercent:F1}% of radius\n" +
                         $"------------------------";

            Dictionary<string, double>? offset = null;
            if (circles.Count >= 2)
            {
                var outer = circles.MaxBy(c => c.Radius)!;
                var inner = circles.MinBy(c => c.Radius)!;
                double dx = inner.X - outer.X;
                double dy = inner.Y - outer.Y;
                double magnitude = Math.Sqrt(dx * dx + dy * dy) / Math.Max(1, outer.Radius);
                double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                offset = new Dictionary<string, double> { { "ComaMagnitude", magnitude }, { "ComaAngle", angle } };

                desc += $"\nInner/outer offset: {magnitude * 100:F1}% of radius\nOffset angle: {angle:F1}°";
                Cv2.Line(drawnImage, new Point(outer.X, outer.Y), new Point(inner.X, inner.Y), new Scalar(0, 255, 0, 255), 2, LineTypes.AntiAlias);
            }

            return (desc, drawnImage, offset);
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
                    ScrewInstructions.Add(new ScrewInstruction { Id = adj.Id });
                }
            }

            for (int i = 0; i < adjustments.Count; i++)
            {
                ScrewInstructions[i].Color = screwVM.ItemColor;
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
