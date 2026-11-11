using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CollimationCircles.Messages;
using CollimationCircles.Models;
using CollimationCircles.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using HanumanInstitute.MvvmDialogs;
using HanumanInstitute.MvvmDialogs.FrameworkDialogs;
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

        private const int ReferenceWidth = 640;
        private const int AnalysisIntervalMs = 200;
        private const double MinArrowLength = 40;
        private const int MaxDetectedCircles = 8;
        private const double MinCircleRadiusFraction = 20.0 / ReferenceWidth;
        private const double MinEdgeSupport = 0.6;

        private readonly SettingsViewModel settingsViewModel;
        private readonly ICameraService cameraService;
        private readonly ScrewCalibrationViewModel calibration;

        [ObservableProperty]
        private ObservableCollection<ScrewInstruction> screwInstructions = [];

        [ObservableProperty]
        bool isStreaming = false;

        [ObservableProperty]
        bool isCalibrating = false;

        [ObservableProperty]
        bool isLiveAnalysisActive = false;

        [ObservableProperty]
        bool isLiveRmseActive = false;

        [ObservableProperty]
        bool isInsideFocus;

        partial void OnIsInsideFocusChanged(bool value)
        {
            settingsViewModel.IsInsideFocus = value;
            logger.Info("Focus side changed: {0}", value ? "inside focus" : "outside focus");
            RefreshScrewInstructions();
        }

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
            calibration = Ioc.Default.GetRequiredService<ScrewCalibrationViewModel>();
            isInsideFocus = settingsViewModel.IsInsideFocus;
            calibration.IsAnyLiveAnalysisActive = () => IsAnyLiveAnalysisActive;
            calibration.IsAdvancedAnalysisActive = () => IsLiveAnalysisActive;
            calibration.CalibrationChanged += RefreshScrewInstructions;
            calibration.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ScrewCalibrationViewModel.IsCalibrating))
                {
                    IsCalibrating = calibration.IsCalibrating;
                }
            };

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
                        IsLiveAnalysisActive = false;
                        IsLiveRmseActive = false;
                        break;
                    case CameraState.Playing:
                        IsStreaming = true;
                        break;
                }
            });
        }

        private const double MaxGuidedStepTurns = 0.125;
        const bool IsGuidedMode = true;

        [ObservableProperty]
        string guidanceText = string.Empty;
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
                calibration.InvalidateIfScrewChanged();
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
                PostScrewInstructions(lastScrewInput, record: false);
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

        private bool overlaysAligned;

        private void AutoAlignOverlays(Dictionary<string, double>? data)
        {
            if (overlaysAligned || data is null
                || !data.TryGetValue("CenterX", out var cx) || !data.TryGetValue("CenterY", out var cy)
                || !data.TryGetValue("ImageWidth", out var imgW) || !data.TryGetValue("ImageHeight", out var imgH)
                || imgW <= 0 || imgH <= 0)
            {
                return;
            }

            overlaysAligned = true;
            double viewW = settingsViewModel.DockInMainWindow
                ? settingsViewModel.MainWindowWidth - settingsViewModel.SettingsWindowWidth / 2 + 9
                : settingsViewModel.MainWindowWidth;
            double viewH = settingsViewModel.MainWindowHeight;
            double uniform = Math.Min(viewW / imgW, viewH / imgH);
            int offX = (int)Math.Round((cx - imgW / 2) * uniform);
            int offY = (int)Math.Round((cy - imgH / 2) * uniform);
            logger.Info($"Auto-align overlays: center=({cx:F1},{cy:F1}) image={imgW}x{imgH} view={viewW:F0}x{viewH:F0} offset=({offX},{offY})");

            Dispatcher.UIThread.Post(() =>
            {
                settingsViewModel.GlobalOffsetX = Math.Clamp(offX, settingsViewModel.OffsetMin, settingsViewModel.OffsetMax);
                settingsViewModel.GlobalOffsetY = Math.Clamp(offY, settingsViewModel.OffsetMin, settingsViewModel.OffsetMax);
            });
        }

        private void HandleModeSwitch(bool anyActive)
        {
            if (anyActive)
            {
                overlaysAligned = false;
            }

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

            var image = fullImage;

            var descriptions = new List<string>();
            Mat? overlay = null;

            try
            {
                if (IsLiveAnalysisActive)
                {
                    var advanced = RunAdvancedAnalysis(image, IsInsideFocus);
                    descriptions.Add(advanced.Description);
                    overlay = advanced.Image;
                    if (advanced.Wavefront is { } wf)
                    {
                        AutoAlignOverlays(new Dictionary<string, double>
                        {
                            { "CenterX", wf["GeometricX"] },
                            { "CenterY", wf["GeometricY"] },
                            { "ImageWidth", image.Width },
                            { "ImageHeight", image.Height }
                        });
                    }
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
                    AutoAlignOverlays(rmse.Offset);
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

        private string lastGuidanceLog = string.Empty;

        private void LogGuidance(Dictionary<string, double> wavefront, ScrewViewModel screwVM, bool calibrated, List<ScrewAdjustment> adjustments, string guidance)
        {
            double mag = wavefront["ComaMagnitude"];
            double ang = wavefront["ComaAngle"];
            double rad = ang * Math.PI / 180.0;
            string adj = string.Join(", ", adjustments.Select(a => $"S{a.Id}={a.Direction} {a.Turns:F3}"));
            string signature = $"{calibrated}|{adj}|{guidance}";
            if (signature == lastGuidanceLog)
            {
                return;
            }

            lastGuidanceLog = signature;
            logger.Info("Guidance: mode={0}, source={1}, offset magnitude={2:F4}, angle={3:F1} deg, offset=({4:F4},{5:F4}), screws={6}, rotation={7:F1}, adjustments=[{8}], text=\"{9}\"",
                IsLiveAnalysisActive ? (IsInsideFocus ? "advanced/inside" : "advanced/outside") : "rmse",
                calibrated ? "calibrated" : "geometric-estimate",
                mag, ang, mag * Math.Cos(rad), mag * Math.Sin(rad),
                screwVM.Count, screwVM.RotationAngle, adj, guidance);
        }

        private void PostScrewInstructions(Dictionary<string, double>? wavefront, bool record = true)
        {
            lastScrewInput = wavefront;
            if (record)
            {
                calibration.RecordOffset(wavefront);
            }
            var screwVM = settingsViewModel.Items.OfType<ScrewViewModel>().FirstOrDefault();
            if (screwVM is null || wavefront is null)
            {
                Dispatcher.UIThread.Post(ScrewInstructions.Clear);
                return;
            }

            List<ScrewAdjustment>? adjustments = null;
            var response = calibration.GetResponse(screwVM.Count, IsLiveAnalysisActive);
            if (response is not null)
            {
                double rad = wavefront["ComaAngle"] * Math.PI / 180.0;
                double mag = wavefront["ComaMagnitude"];
                adjustments = CalculateCalibratedAdjustments(mag * Math.Cos(rad), mag * Math.Sin(rad), response);
            }

            adjustments ??= CalculateScrewAdjustments(
                wavefront["ComaMagnitude"],
                wavefront["ComaAngle"],
                screwVM.RotationAngle,
                screwVM.Count);

            string guidance = string.Empty;
            if (IsGuidedMode)
            {
                var guidedResponse = calibration.GetResponse(screwVM.Count, IsLiveAnalysisActive)
                    ?? BuildEstimatedResponse(screwVM.RotationAngle, screwVM.Count);
                double guidedRad = wavefront["ComaAngle"] * Math.PI / 180.0;
                double guidedMag = wavefront["ComaMagnitude"];
                var best = CalculateBestSubsetAdjustment(guidedMag * Math.Cos(guidedRad), guidedMag * Math.Sin(guidedRad), guidedResponse) ?? adjustments;

                double maxTurns = best.Where(a => a.Direction != "Fixed").Select(a => a.Turns).DefaultIfEmpty(0).Max();
                double factor = maxTurns > MaxGuidedStepTurns ? MaxGuidedStepTurns / maxTurns : 1.0;
                adjustments = best.Select(a => a.Direction == "Fixed"
                    ? a
                    : new ScrewAdjustment { Id = a.Id, Direction = a.Direction, Magnitude = a.Magnitude * factor, Turns = a.Turns * factor }).ToList();

                if (wavefront.TryGetValue("OffsetPx", out var offsetPx) && offsetPx < GetDeadZonePx())
                {
                    adjustments = adjustments.Select(a => new ScrewAdjustment { Id = a.Id, Direction = "Fixed", Magnitude = 0, Turns = 0 }).ToList();
                }

                var activeIds = adjustments.Where(a => a.Direction != "Fixed").Select(a => a.Id).ToList();
                guidance = activeIds.Count switch
                {
                    0 => "Error is minimal. Stop adjusting.",
                    1 => $"Turn only Screw {activeIds[0]}, then wait for the reading to settle and re-check.",
                    _ => $"Turn only Screws {string.Join(" and ", activeIds)} together, then wait for the reading to settle and re-check."
                };
            }

            var finalAdjustments = adjustments;
            LogGuidance(wavefront, screwVM, response is not null, finalAdjustments, guidance);
            Dispatcher.UIThread.Post(() =>
            {
                GuidanceText = guidance;
                UpdateScrewInstructions(finalAdjustments, screwVM);
            });
        }

        private static double ScaleOf(Mat image) => Math.Max(1.0, (double)image.Width / ReferenceWidth);

        private static int OddKernel(int baseSize, double scale)
        {
            int k = (int)Math.Round(baseSize * scale);
            return k % 2 == 0 ? k + 1 : k;
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
            double scale = ScaleOf(image);
            int blurK = OddKernel(7, scale);
            Cv2.GaussianBlur(gray, gray, new Size(blurK, blurK), 2 * scale);

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
                        Votes = 0,
                        CenterX = ellipse.Center.X,
                        CenterY = ellipse.Center.Y,
                        ExactRadius = radiusFit
                    }, hullArea));
                }
            }

            foreach (var m in masks)
            {
                m.Dispose();
            }

            int mergeTolerance = (int)Math.Round(6 * scale);
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

        private static (string Description, Dictionary<string, double>? Wavefront, Mat Image) RunAdvancedAnalysis(Mat image, bool insideFocus)
        {
            using Mat gray = new();
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
            int advBlur = OddKernel(5, ScaleOf(image));
            Cv2.GaussianBlur(gray, gray, new Size(advBlur, advBlur), 0);
            Cv2.Normalize(gray, gray, 0, 255, NormTypes.MinMax);

            var wavefront = AnalyzeStarWavefront(gray, insideFocus);
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
                DrawAlignmentMarkers(drawImage, new Point((int)x, (int)y), new Point((int)wavefront["CentroidX"], (int)wavefront["CentroidY"]),
                    wavefront["ComaMagnitude"], $"{(insideFocus ? "In" : "Out")} focus  Offset {wavefront["ComaMagnitude"] * 100:F1}%  {wavefront["ComaAngle"]:F0} deg", ScaleOf(image));
            }

            return (DescribeAdvancedResult(wavefront), wavefront, drawImage);
        }

        private int GetCurrentBinning()
        {
            var stream = Ioc.Default.GetService<StreamViewModel>();
            var control = stream?.SelectedCamera?.Controls?.FirstOrDefault(c => c.Name == ControlType.Binning);
            return control is null ? 1 : Math.Max(1, (int)Math.Round(control.Value));
        }

        private double GetBinnedPixelUm() => settingsViewModel.CameraPixelSizeUm * GetCurrentBinning();

        private double GetDeadZonePx()
        {
            double deadZone = 1.0;
            double pixelUm = GetBinnedPixelUm();
            if (pixelUm > 0 && settingsViewModel.TelescopeApertureMm > 0 && settingsViewModel.TelescopeFocalLengthMm > 0)
            {
                double fRatio = settingsViewModel.TelescopeFocalLengthMm / settingsViewModel.TelescopeApertureMm;
                double toleranceUm = 4.88 * 0.55 * fRatio * fRatio;
                deadZone = Math.Max(deadZone, toleranceUm / pixelUm);
            }

            return deadZone;
        }

        private string DescribeOffsetScale(double offsetPx)
        {
            string text = $"\nOffset: {offsetPx:F1} px";
            double pixelUm = GetBinnedPixelUm();
            if (pixelUm > 0)
            {
                text += $" ({offsetPx * pixelUm / 1000.0:F3} mm at sensor)";
                if (settingsViewModel.TelescopeFocalLengthMm > 0)
                {
                    text += $", {offsetPx * 206.265 * pixelUm / settingsViewModel.TelescopeFocalLengthMm:F1} arcsec";
                }
            }

            text += $"\nDead zone: {GetDeadZonePx():F1} px";
            return text;
        }

        private (string Description, Mat Image, Dictionary<string, double>? Offset) RunRmseAnalysis(Mat image)
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
            if (circles.Count < 2)
            {
                desc += $"\nNot enough circles detected ({circles.Count} of 2 needed). Adjust exposure/gain or camera position.";
            }

            if (circles.Count >= 2)
            {
                var outer = circles.MaxBy(c => c.ExactRadius)!;
                var inner = circles.MinBy(c => c.ExactRadius)!;
                double dx = inner.CenterX - outer.CenterX;
                double dy = inner.CenterY - outer.CenterY;
                double magnitude = Math.Sqrt(dx * dx + dy * dy) / Math.Max(1, outer.ExactRadius);
                double angle = Math.Atan2(dy, dx) * 180.0 / Math.PI;
                double offsetPx = Math.Sqrt(dx * dx + dy * dy);
                offset = new Dictionary<string, double> { { "ComaMagnitude", magnitude }, { "ComaAngle", angle }, { "OffsetPx", offsetPx }, { "CenterX", outer.CenterX }, { "CenterY", outer.CenterY }, { "ImageWidth", image.Width }, { "ImageHeight", image.Height } };

                desc += $"\nInner/outer offset: {magnitude * 100:F1}% of radius\nOffset angle: {angle:F1}°";
                desc += DescribeOffsetScale(offsetPx);
                Cv2.Line(drawnImage, new Point(outer.X, outer.Y), new Point(inner.X, inner.Y), new Scalar(0, 255, 0, 255), 2, LineTypes.AntiAlias);

                DrawAlignmentMarkers(drawnImage, new Point(outer.X, outer.Y), new Point(inner.X, inner.Y), magnitude,
                    $"RMSE {analysis.CenterRMSEPercent:F1}%  {angle:F0} deg", ScaleOf(image));
            }

            return (desc, drawnImage, offset);
        }

        private static void DrawAlignmentMarkers(Mat img, Point target, Point marker, double magnitude, string label, double s)
        {
            bool nearlyAligned = magnitude * 100 < 1.0;
            var markColor = nearlyAligned ? new Scalar(0, 255, 0, 255) : new Scalar(0, 165, 255, 255);
            var targetColor = new Scalar(255, 255, 0, 255);

            int arm = (int)Math.Round(14 * s);
            Cv2.Line(img, new Point(target.X - arm, target.Y), new Point(target.X + arm, target.Y), targetColor, 2, LineTypes.AntiAlias);
            Cv2.Line(img, new Point(target.X, target.Y - arm), new Point(target.X, target.Y + arm), targetColor, 2, LineTypes.AntiAlias);
            Cv2.Circle(img, target, (int)Math.Round(8 * s), targetColor, 2, LineTypes.AntiAlias);

            Cv2.Circle(img, marker, (int)Math.Round(10 * s), markColor, 2, LineTypes.AntiAlias);
            Cv2.Circle(img, marker, Math.Max(2, (int)Math.Round(2 * s)), markColor, -1, LineTypes.AntiAlias);

            double fontScale = 0.5 * s;
            int thickness = Math.Max(1, (int)Math.Round(s));
            var textSize = Cv2.GetTextSize(label, HersheyFonts.HersheySimplex, fontScale, thickness, out int baseline);
            int tx = Math.Clamp(marker.X + (int)Math.Round(14 * s), 4, Math.Max(4, img.Width - textSize.Width - 4));
            int ty = Math.Clamp(marker.Y - (int)Math.Round(14 * s), textSize.Height + 4, img.Height - 4);
            Cv2.Rectangle(img, new Point(tx - 3, ty - textSize.Height - 3), new Point(tx + textSize.Width + 3, ty + baseline + 1), new Scalar(0, 0, 0, 200), -1);
            Cv2.PutText(img, label, new Point(tx, ty), HersheyFonts.HersheySimplex, fontScale, markColor, thickness, LineTypes.AntiAlias);
        }        

        private static string DescribeAdvancedResult(Dictionary<string, double>? wavefront)
        {
            if (wavefront is null)
            {
                return "Star not detected\n";
            }

            return $"Focus: {(wavefront["InsideFocus"] > 0 ? "inside" : "outside")}\n" +
                   $"Disc radius: {wavefront["Radius"]:F1}px\n" +
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
        [NotifyPropertyChangedFor(nameof(DisplayText))]
        [NotifyPropertyChangedFor(nameof(IsActive))]
        private string direction = string.Empty;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DisplayText))]
        [NotifyPropertyChangedFor(nameof(IsActive))]
        private double turns;
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Brush))]
        private Color color;

        public IBrush Brush => new SolidColorBrush(Color);

        public bool IsActive => DisplayText != "Locked";

        public string DisplayText => FormatTurns(Direction, Turns);

        private static string FormatTurns(string direction, double turns)
        {
            const int steps = 16;
            double exactDegrees = turns * 360.0;
            if (direction == "Fixed" || exactDegrees < 2)
            {
                return "Locked";
            }

            string way = direction == "CW" ? "CW" : "CCW";
            int sixteenths = (int)Math.Round(turns * steps);
            if (sixteenths == 0)
            {
                return $"{way} small nudge (~{exactDegrees:F0}°)";
            }
            int whole = sixteenths / steps;
            int rest = sixteenths % steps;

            string amount;
            if (rest == 0)
            {
                amount = whole.ToString();
            }
            else
            {
                int gcd = Gcd(rest, steps);
                string frac = $"{rest / gcd}/{steps / gcd}";
                amount = whole > 0 ? $"{whole} {frac}" : frac;
            }

            double degrees = sixteenths * 360.0 / steps;
            string unit = sixteenths <= steps ? "turn" : "turns";
            return $"{way} {amount} {unit} (~{degrees:F0}°)";
        }

        private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);
    }
}
