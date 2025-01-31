using CommunityToolkit.Diagnostics;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace CollimationCircles.Services
{
    internal static class ImageAnalysisService
    {
        public enum AnalysisType
        {
            ContourMinimumEnclosingCircle,
            CircleHoughTransform
        }

        public class Options
        {
            public double MinConturArea { get; internal set; } = 1000;
            public int MinCircleRadius { get; internal set; } = 30;
            public int MaxCircleRadius { get; internal set; } = 2000;
            public int MinCirclesDetected { get; set; } = 2;
            public int OffsetLimit { get; set; } = 5;
            public bool DoGrayscale { get; set; } = true;
            public bool DoNormalize { get; set; } = true;
            public bool DoMedianBlur { get; set; } = false;
            public bool DoGaussianBlur { get; set; } = true;
            public bool DoBilateralFilter { get; internal set; } = false;
            public bool DoAdaptiveThreshold { get; set; } = false;
            public bool DoMorphologyEx { get; set; } = false;
            public bool DoCanny { get; set; } = false;
            public string ImagesPath { get; set; } = ".\\";
            public bool SaveImages { get; set; } = false;
            public bool ShowEachImage { get; set; } = false;
            public NormalizeOptions NormalizeOptions { get; set; } = new();
            public BlurOptions BlurOptions { get; set; } = new();
            public BilateralFilter BilateralFilter { get; set; } = new();
            public AdaptiveThresholdOptions AdaptiveThresholdOptions { get; set; } = new();
            public MorphologyExOptions MorphologyExOptions { get; set; } = new();
            public CannyOptions CannyOptions { get; set; } = new();
            public FindContoursOptions FindContoursOptions { get; set; } = new();            
        }

        public class NormalizeOptions
        {
            public double Aplha { get; set; } = 0;
            public double Beta { get; set; } = 255;
            public NormType NormType { get; set; } = NormType.MinMax;
            public double DType { get; set; } = -1;
        }

        public class BlurOptions
        {
            public int KSize { get; set; } = 11;
            public int SigmaX { get; set; } = 11;
            public double SigmaY { get; internal set; } = 0;
            public BorderType BorderType { get; internal set; }
        }

        public class BilateralFilter
        {
            public int D { get; set; } = 15;
            public double SigmaColor { get; set; } = 80;
            public double SigmaSpace { get; set; } = 80;
            public BorderType BorderType { get; internal set; } = BorderType.Default;
        }

        public class AdaptiveThresholdOptions
        {
            public double MaxValue { get; set; } = 20;
            public AdaptiveThresholdType AdaptiveThresholdType { get; set; } = AdaptiveThresholdType.GaussianC;
            public ThresholdType ThresholdType { get; set; } = ThresholdType.Binary;
            public int BlockSize { get; set; } = 51;
            public double C { get; set; } = 0;
        }

        public class MorphologyExOptions
        {
            public ElementShape MorphShape { get; set; } = ElementShape.Ellipse;
            public int Size { get; set; } = 5;
            public MorphOp MorphType { get; set; } = MorphOp.Open;
            public int Iterations { get; set; } = 3;
            public Point Ancor { get; set; }
            public BorderType BorderType { get; internal set; } = BorderType.Constant;
            public MCvScalar BorderValue { get; internal set; }
        }

        public class CannyOptions
        {
            public double Threshold1 { get; set; } = 0;
            public double Threshold2 { get; set; } = 50;
            public int ApertureSize { get; set; } = 3;
            public bool LGradient2 { get; set; } = false;
        }

        public class FindContoursOptions
        {
            public RetrType RetrievalMode { get; set; } = RetrType.List;
            public ChainApproxMethod ContourApproximationMode { get; set; } = ChainApproxMethod.ChainApproxSimple;
            public MCvScalar Color { get; set; } = new MCvScalar(255, 255, 255);
            public int Thickness { get; set; } = 2;
        }

        public class AnalysisResult
        {
            public double? Offset { get; set; } = null;
            public string? Error { get; set; } = null;
            public int? CircleCount { get; set; } = null;
        }

        public static byte[] FileToByteArray(string fileName)
        {
            FileStream fs = new(fileName, FileMode.Open, FileAccess.Read);
            BinaryReader br = new(fs);
            long numBytes = new FileInfo(fileName).Length;
            byte[] buff = br.ReadBytes((int)numBytes);
            return buff;
        }

        public static Mat LoadImage(string sourceImageFile, ImreadModes imreadModes)
        {
            Mat image = CvInvoke.Imread(sourceImageFile, imreadModes);

            return image;
        }

        public static Mat LoadImage(byte[] sourceImageBytes, ImreadModes imreadModes)
        {
            Mat image = new();
                
            CvInvoke.Imdecode(sourceImageBytes, imreadModes, image);

            return image;
        }        

        public static Mat ProcessImage(Mat image, Options options)
        {
            Mat processed = new();
            image.CopyTo(processed);

            string path = options.ImagesPath;

            if (options.DoGrayscale)
            {
                CvInvoke.CvtColor(processed, processed, ColorConversion.Bgr2Gray);
            }

            if (options.DoNormalize)
            {
                // Normalize brightness and contrast
                CvInvoke.Normalize(processed, processed,
                    options.NormalizeOptions.Aplha,
                    options.NormalizeOptions.Beta,
                    options.NormalizeOptions.NormType);

                if (options.SaveImages) processed.Save($"{path}0_Normalize.jpg");
                if (options.ShowEachImage) CvInvoke.Imshow("Step 1: Normalize", processed);
            }

            if (options.DoMedianBlur)
            {
                // Apply Median blur                
                CvInvoke.MedianBlur(processed, processed,
                    options.BlurOptions.KSize);

                if (options.SaveImages) processed.Save($"{path}1_MedianBlur.jpg");
                if (options.ShowEachImage) CvInvoke.Imshow("Step 2: Median blur", processed);
            }

            if (options.DoGaussianBlur)
            {
                // Apply Median blur                
                CvInvoke.GaussianBlur(processed, processed,
                    new Size(options.BlurOptions.KSize, options.BlurOptions.KSize),
                    options.BlurOptions.SigmaX,
                    options.BlurOptions.SigmaY,
                    options.BlurOptions.BorderType);

                if (options.SaveImages) processed.Save($"{path}1a_GaussianBlur.jpg");
                if (options.ShowEachImage) CvInvoke.Imshow("Step 3: Gaussian blur", processed);
            }

            if (options.DoBilateralFilter)
            {
                CvInvoke.BilateralFilter(processed, processed,
                    options.BilateralFilter.D,
                    options.BilateralFilter.SigmaColor,
                    options.BilateralFilter.SigmaSpace,
                    options.BilateralFilter.BorderType);

                if (options.SaveImages) processed.Save($"{path}1b_BilateralFilter.jpg");
                if (options.ShowEachImage) CvInvoke.Imshow("Step 4: Bilateral filter", processed);
            }

            if (options.DoAdaptiveThreshold)
            {
                // Apply Adaptive threshold
                CvInvoke.AdaptiveThreshold(processed, processed,
                    options.AdaptiveThresholdOptions.MaxValue,
                    options.AdaptiveThresholdOptions.AdaptiveThresholdType,
                    options.AdaptiveThresholdOptions.ThresholdType,
                    options.AdaptiveThresholdOptions.BlockSize,
                    options.AdaptiveThresholdOptions.C);

                if (options.SaveImages) processed.Save($"{path}2_AdaptiveThreshold.jpg");
                if (options.ShowEachImage) CvInvoke.Imshow("Step 5: Adaptive thershold", processed);
            }

            if (options.DoMorphologyEx)
            {
                // Morph open 
                Mat kernel = CvInvoke.GetStructuringElement(
                    options.MorphologyExOptions.MorphShape,
                    new Size(options.MorphologyExOptions.Size, options.MorphologyExOptions.Size),
                    options.MorphologyExOptions.Ancor);

                CvInvoke.MorphologyEx(processed, processed,
                    options.MorphologyExOptions.MorphType,
                    kernel,
                    options.MorphologyExOptions.Ancor,
                    options.MorphologyExOptions.Iterations,
                    options.MorphologyExOptions.BorderType,
                    options.MorphologyExOptions.BorderValue);

                if (options.SaveImages) processed.Save($"{path}3_MorphologyEx.jpg");
                if (options.ShowEachImage) CvInvoke.Imshow("Step 6: MorphologyEx", processed);
            }

            if (options.DoCanny)
            {
                // Apply Canny edge detector
                CvInvoke.Canny(processed, processed,
                    options.CannyOptions.Threshold1,
                    options.CannyOptions.Threshold2,
                    options.CannyOptions.ApertureSize,
                    options.CannyOptions.LGradient2);

                if (options.SaveImages) processed.Save($"{path}4_Canny.jpg");
                if (options.ShowEachImage) CvInvoke.Imshow("Step 7: Canny", processed);
            }

            return processed;
        }

        public static List<CircleF> DetectCirclesFromContour(Mat image, Options options)
        {
            List<CircleF> circles = [];

            // Compute image center
            PointF imageCenter = new(image.Width / 2, image.Height / 2);

            using (VectorOfVectorOfPoint contours = new VectorOfVectorOfPoint())
            {
                //IOutputArray hierarchy = new Mat();

                // Find contours
                CvInvoke.FindContours(image, contours, null,
                    options.FindContoursOptions.RetrievalMode,
                    options.FindContoursOptions.ContourApproximationMode);

                options.MinConturArea = 1000;
                options.OffsetLimit = 20;

                // Fit circles to contours
                //int count = contours.Size;
                for (int i = 0; i < contours.Size; i++)
                {
                    using VectorOfPoint contour = contours[i];
                    using VectorOfPoint approxContour = new VectorOfPoint();

                    double area = CvInvoke.ContourArea(contour);

                    if (area > options.MinConturArea && area < image.Width * image.Height) // Filter based on area
                    {
                        CircleF circle = CvInvoke.MinEnclosingCircle(contour);

                        // Compute Euclidean distance (offset) between the average circle center and the image center
                        // var distance = Math. Sqrt. Pow(x1 - x2, 2) + Math. Pow(y1 - y2, 2)));
                        double offset = Math.Sqrt(
                            Math.Pow(circle.Center.X - imageCenter.X, 2) +
                            Math.Pow(circle.Center.Y - imageCenter.Y, 2)
                        );

                        if (offset < options.OffsetLimit)
                        {
                            circles.Add(new CircleF(circle.Center, circle.Radius));
                        }
                    }
                }
            }

            return circles;
        }

        public static List<CircleF> DetectHoughCircles(Mat image, Options options)
        {
            Guard.IsNotNull(image);
            Guard.IsNotNull(options);

            // Compute image center
            PointF imageCenter = new(image.Width / 2, image.Height / 2);

            List<CircleF> circlesAll = [];

            for (int i = options.MinCircleRadius; i <= options.MaxCircleRadius; i += 10)
            {
                // Use HoughCircles to detect circles
                CircleF[] circles = CvInvoke.HoughCircles(
                    image,
                    HoughModes.Gradient,
                    dp: 1,
                    minDist: 40,
                    param1: 100,
                    param2: 30,
                    minRadius: i,
                    maxRadius: 2 * i
                );

                foreach (CircleF circle in circles)
                {
                    if (!circlesAll.Any(x => x.Radius == circle.Radius && x.Center == circle.Center))
                    {
                        // Compute Euclidean distance (offset) between the average circle center and the image center
                        double offset = Math.Sqrt(
                            Math.Pow(circle.Center.X - imageCenter.X, 2) +
                            Math.Pow(circle.Center.Y - imageCenter.Y, 2)
                        );

                        if (offset < options.OffsetLimit)
                        {
                            circlesAll.Add(circle);
                        }
                    }
                }
            }

            return circlesAll;
        }

        public static AnalysisResult AnalyzeResult(Mat image, List<CircleF> circles, Options options)
        {
            string? error = null;
            double? offset = null;

            RNG rng = new(12345);
            // Draw detected circles on the image
            foreach (CircleF circle in circles)
            {                
                MCvScalar color = new(rng.Uniform(0, 255), rng.Uniform(0, 255), rng.Uniform(0, 255));
                CvInvoke.Circle(image, new Point((int)circle.Center.X, (int)circle.Center.Y), (int)circle.Radius, color, 2);
                CvInvoke.Circle(image, new Point((int)circle.Center.X, (int)circle.Center.Y), 2, color, -1);
            }            

            if (circles.Count == 0)
            {
                error = "No circles detected in image.";
            }
            else
            {
                // Compute average center of circles
                var avgCenter = new PointF(
                    circles.Average(c => c.Center.X),
                    circles.Average(c => c.Center.Y)
                );

                // Compute image center
                PointF imageCenter = new(image.Width / 2, image.Height / 2);

                // Compute Euclidean distance (offset) between the average circle center and the image center
                offset = Math.Sqrt(
                    Math.Pow(avgCenter.X - imageCenter.X, 2) +
                    Math.Pow(avgCenter.Y - imageCenter.Y, 2)
                );
            }            

            return new AnalysisResult
            {
                Error = error,
                CircleCount = circles.Count,
                Offset = offset
            };
        }
    }
}
