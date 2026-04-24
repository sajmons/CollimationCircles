using Avalonia.Media;
using Avalonia.Media.Imaging;
using CollimationCircles.Messages;
using CollimationCircles.Services;
using CollimationCirclesFeatures;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Messaging;
using ImageMagick;
using ImageMagick.Drawing;
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

        private readonly ILibVLCService libVLCService;
        private readonly SettingsViewModel settingsViewModel;
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

        public CollimationAnalysisViewModel(ILibVLCService libVLCService)
        {
            this.libVLCService = libVLCService;
            this.settingsViewModel = Ioc.Default.GetRequiredService<SettingsViewModel>();

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
            await CheckFeatureLicensed(FeatureList.LiveAnalysis, () =>
            {
                HandleModeSwitch(value || IsLiveRmseActive);
            });
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
                    byte[]? data = await libVLCService.TakeSnapshotDataAsync();
                    if (data != null)
                    {
                        using var image = new MagickImage(data);

                        string description = string.Empty;
                        MagickImage? advancedImage = null;
                        MagickImage? rmseImage = null;

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
                            using var stream = new MemoryStream();
                            resultImage.Alpha(AlphaOption.Set);
                            resultImage.Write(stream, MagickFormat.Png32);
                            stream.Position = 0;
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

        private static (string Description, Dictionary<string, double>? Wavefront, CircularFeature? Circle, MagickImage Image) RunAdvancedAnalysis(MagickImage image)
        {
            using Mat frame = ConvertMagickToMat(image);
            using Mat gray = new();
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);

            var edges = FindEdgePoints(gray);
            var refinedEdges = OpticalAxisService.RefineEdgesSubpixel(gray, edges);
            var circle = OpticalAxisService.FitCircleRobust(refinedEdges);
            var wavefront = OpticalAxisService.AnalyzeStarWavefront(gray);

            // Classic circles for visualization
            Options classicOptions = new() { DoNormalize = true, DoGaussianBlur = true, DoThreshold = true, DoEdge = true };
            using var copy = new MagickImage(image);
            ImageAnalysisService.ProcessImage(copy, classicOptions);
            var classicCircles = ImageAnalysisService.DetectCircles(copy, 50, (int)image.Width / 2, new DetectionParameters { VoteThresholdFraction = 0.8 });

            MagickImage drawImage = new(MagickColors.None, (uint)image.Width, (uint)image.Height);
            drawImage.Alpha(AlphaOption.Transparent);

            // Draw classic circles (Hough) in yellow
            var drawables = new Drawables().StrokeColor(MagickColors.Yellow).StrokeWidth(1).FillColor(MagickColors.Transparent);
            foreach (var c in classicCircles)
            {
                drawables.Circle(c.X, c.Y, c.X + c.Radius, c.Y);
            }

            // Draw Robust Sub-pixel Circle in Cyan
            if (circle != null)
            {
                drawables.StrokeColor(MagickColors.Cyan).StrokeWidth(2);
                drawables.Circle(circle.Center.X, circle.Center.Y, circle.Center.X + circle.Radius, circle.Center.Y);
            }

            // Draw Coma Vector in Lime
            if (wavefront != null && circle != null)
            {
                DrawVector(drawImage, (int)wavefront["GeometricX"], (int)wavefront["GeometricY"],
                    (int)(wavefront["GeometricX"] + wavefront["ComaMagnitude"] * circle.Radius * 5.0 * Math.Cos(wavefront["ComaAngle"] * Math.PI / 180.0)),
                    (int)(wavefront["GeometricY"] + wavefront["ComaMagnitude"] * circle.Radius * 5.0 * Math.Sin(wavefront["ComaAngle"] * Math.PI / 180.0)));
            }

            drawImage.Draw(drawables);

            string desc = DescribeAdvancedResult(wavefront, circle);
            return (desc, wavefront, circle, drawImage);
        }

        private static (string Description, MagickImage Image) RunRmseAnalysis(MagickImage image)
        {
            Options options = new() { DoNormalize = true, DoGaussianBlur = true, DoThreshold = true, DoEdge = true };
            using var copy = new MagickImage(image);
            ImageAnalysisService.ProcessImage(copy, options);
            var circles = ImageAnalysisService.DetectCircles(copy, 50, (int)image.Width / 2, new DetectionParameters { VoteThresholdFraction = 0.8 });

            MagickImage drawnImage = new(MagickColors.None, (uint)image.Width, (uint)image.Height);
            drawnImage.Alpha(AlphaOption.Transparent);
            var analysis = ImageAnalysisService.AnalyzeResult(drawnImage, circles, options);

            string desc = $"--- MECHANICAL RMSE ---\n" +
                         $"Circles: {analysis.CircleCount}\n" +
                         $"RMSE: {analysis.CenterRMSE:F2}\n" +
                         $"------------------------";

            return (desc, drawnImage);
        }

        private static void DrawVector(MagickImage img, int x1, int y1, int x2, int y2)
        {
            var drawables = new Drawables()
                .StrokeColor(MagickColors.Lime)
                .StrokeWidth(3)
                .Line(x1, y1, x2, y2)
                .FillColor(MagickColors.Lime)
                .Circle(x1, y1, x1 + 4, y1); // Dot at geometric center

            // Draw arrowhead
            double angle = Math.Atan2(y2 - y1, x2 - x1);
            double arrowSize = 15;
            int ax1 = (int)(x2 - arrowSize * Math.Cos(angle - Math.PI / 6));
            int ay1 = (int)(y2 - arrowSize * Math.Sin(angle - Math.PI / 6));
            int ax2 = (int)(x2 - arrowSize * Math.Cos(angle + Math.PI / 6));
            int ay2 = (int)(y2 - arrowSize * Math.Sin(angle + Math.PI / 6));

            drawables.Line(x2, y2, ax1, ay1);
            drawables.Line(x2, y2, ax2, ay2);

            img.Draw(drawables);
        }

        private static Mat ConvertMagickToMat(MagickImage image)
        {
            // Convert MagickImage to OpenCV Mat via Byte Array
            byte[] pixels = image.ToByteArray(MagickFormat.Bgr);
            Mat mat = new((int)image.Height, (int)image.Width, MatType.CV_8UC3);
            System.Runtime.InteropServices.Marshal.Copy(pixels, 0, mat.Data, pixels.Length);
            return mat;
        }

        private static List<Point2d> FindEdgePoints(Mat gray)
        {
            Mat edges = new();
            Cv2.Canny(gray, edges, 50, 150);
            List<Point2d> points = [];
            for (int y = 0; y < edges.Height; y++)
            {
                for (int x = 0; x < edges.Width; x++)
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
