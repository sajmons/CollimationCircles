using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    internal static class ImageAnalysisService
    {
        public class DetectionParameters
        {            
            public int CenterProximityThreshold { get; set; } = 40;
            public double VoteThresholdFraction { get; set; } = 0.8;
            public int AngleStep { get; set; } = 5;
            public int RadiusStep { get; set; } = 5;
            public int EdgeThreshold { get; set; } = 128;
        }

        public class FilterComplitedEventArgs(string filterName, byte[] image) : EventArgs
        {
            public string FilterName { get; set; } = filterName;
            public byte[] ImageBytes { get; set; } = image;
        }

        public static event EventHandler<FilterComplitedEventArgs>? FilterComplited;

        public class Circle
        {
            public int X { get; set; }       // X-coordinate of center
            public int Y { get; set; }       // Y-coordinate of center
            public int Radius { get; set; }  // Circle radius
            public int Votes { get; set; }   // Accumulator votes
        }

        public class Options
        {
            public bool DoGrayscale { get; set; } = true;
            public bool DoNormalize { get; set; } = true;
            public bool DoGaussianBlur { get; set; } = true;
            public bool DoThreshold { get; set; } = false;
            public bool DoEdge { get; set; } = false;
            public bool DoDilate { get; internal set; } = false;
            public bool DoErode { get; internal set; } = false;
            public bool DoCrop { get; internal set; } = false;
            public BlurOptions BlurOptions { get; set; } = new();
            public ThresholdOptions AdaptiveThresholdOptions { get; set; } = new();
            public MorphologySettings ErodeSettings { get; set; } = new();
            public MorphologySettings DilateSettings { get; set; } = new();
            public EdgeOptions EdgeSettings { get; set; } = new();
            public Rect? BoundingBox { get; internal set; }
        }

        public class MorphologySettings
        {
            public int Iterations { get; set; } = 1;
            public int KernelSize { get; set; } = 3;
        }

        public class EdgeOptions
        {
            public double LowThreshold { get; set; } = 50;
            public double HighThreshold { get; set; } = 150;
        }

        public class BlurOptions
        {
            public int Radius { get; set; } = 1;
            public int Sigma { get; set; } = 1;
        }

        public class ThresholdOptions
        {
            /// <summary>Threshold as a percentage (0-100) of the 8-bit range.</summary>
            public double Percentage { get; internal set; } = 20;
        }

        public class AnalysisResult
        {
            public int? CircleCount { get; set; } = null;
            public double CenterRMSE { get; set; } = 0.0;
            /// <summary>Center RMSE as a percentage of the largest detected circle radius (scale independent).</summary>
            public double CenterRMSEPercent { get; set; } = 0.0;
        }

        private static byte[] Encode(Mat image)
        {
            Cv2.ImEncode(".png", image, out var bytes);
            return bytes;
        }

        private static void Notify(string name, Mat image)
        {
            if (FilterComplited is not null)
            {
                FilterComplited.Invoke(null, new FilterComplitedEventArgs(name, Encode(image)));
            }
        }

        public static void ProcessImage(Mat image, Options options)
        {
            if (options.DoGrayscale && image.Channels() > 1)
            {
                var code = image.Channels() == 4 ? ColorConversionCodes.BGRA2GRAY : ColorConversionCodes.BGR2GRAY;
                Cv2.CvtColor(image, image, code);
                Notify("Grayscale", image);
            }

            if (options.DoNormalize)
            {
                Cv2.Normalize(image, image, 0, 255, NormTypes.MinMax);
                Notify("Normalize", image);
            }

            if (options.DoGaussianBlur)
            {
                int k = options.BlurOptions.Radius * 2 + 1;
                Cv2.GaussianBlur(image, image, new Size(k, k), options.BlurOptions.Sigma);
                Notify("GaussianBlur", image);
            }

            if (options.DoThreshold)
            {
                double thresh = 255.0 * options.AdaptiveThresholdOptions.Percentage / 100.0;
                Cv2.Threshold(image, image, thresh, 255, ThresholdTypes.Binary);
                Notify("Threshold", image);
            }

            if (options.DoErode)
            {
                using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(options.ErodeSettings.KernelSize, options.ErodeSettings.KernelSize));
                Cv2.Erode(image, image, kernel, iterations: options.ErodeSettings.Iterations);
                Notify("Morphology/Erode", image);
            }

            if (options.DoDilate)
            {
                using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(options.DilateSettings.KernelSize, options.DilateSettings.KernelSize));
                Cv2.Dilate(image, image, kernel, iterations: options.DilateSettings.Iterations);
                Notify("Morphology/Dilate", image);
            }

            if (options.DoEdge)
            {
                Cv2.Canny(image, image, options.EdgeSettings.LowThreshold, options.EdgeSettings.HighThreshold);
                Notify("Edge", image);
            }

            if (options.DoCrop)
            {
                using Mat gray = image.Channels() > 1 ? image.CvtColor(ColorConversionCodes.BGR2GRAY) : image.Clone();
                var box = Cv2.BoundingRect(gray);
                if (box.Width > 0 && box.Height > 0)
                {
                    options.BoundingBox = box;
                    using var cropped = new Mat(image, box).Clone();
                    cropped.CopyTo(image);
                    Notify("Crop", image);
                }
            }
        }

        /// <summary>
        /// Detect circles in an 8-bit grayscale image using a Hough transform.
        /// Uses a precomputed offset table (per candidate radius) to optimize inner-loop computations.
        /// Only circles with centers near the image center (within centerProximityThreshold)
        /// and whose accumulator value exceeds a given fraction (0 to 1) of the maximum possible votes are returned.
        /// A non-maximum suppression step merges nearby duplicate detections.
        /// </summary>
        /// <param name="edgeImage">
        /// An 8bpp grayscale edge image. Pixels with intensity above edgeThreshold are treated as edges.
        /// </param>
        /// <param name="minRadius">Minimum circle radius to search.</param>
        /// <param name="maxRadius">Maximum circle radius to search.</param>
        /// <param name="detectionParameters">
        /// Detection parameters. See <see cref="DetectionParameters"/> for details.
        /// </param>        
        /// <returns>A list of filtered circles detected that meet the criteria.</returns>
        public static List<Circle> DetectCircles(Mat edgeImage, int minRadius, int maxRadius, DetectionParameters detectionParameters)
        {
            using Mat gray = new();
            if (edgeImage.Channels() > 1)
            {
                Cv2.CvtColor(edgeImage, gray, edgeImage.Channels() == 4 ? ColorConversionCodes.BGRA2GRAY : ColorConversionCodes.BGR2GRAY);
            }
            else
            {
                edgeImage.CopyTo(gray);
            }

            if (gray.Type() != MatType.CV_8UC1)
            {
                gray.ConvertTo(gray, MatType.CV_8UC1);
            }

            int radiusStep = detectionParameters.RadiusStep;
            int angleStep = detectionParameters.AngleStep;
            double voteThresholdFraction = detectionParameters.VoteThresholdFraction;
            int edgeThreshold = detectionParameters.EdgeThreshold;
            int centerProximityThreshold = detectionParameters.CenterProximityThreshold;

            int width = gray.Width;
            int height = gray.Height;
            int radiusCount = ((maxRadius - minRadius) / radiusStep) + 1;
            int[] candidateRadii = new int[radiusCount];
            for (int i = 0; i < radiusCount; i++)
            {
                candidateRadii[i] = minRadius + i * radiusStep;
            }

            // Precompute, for each candidate radius, a list of offset tuples (dx, dy) for each discrete angle.
            int angleCount = 360 / angleStep;
            List<(int dx, int dy)>[] offsetLookup = new List<(int dx, int dy)>[radiusCount];
            for (int i = 0; i < radiusCount; i++)
            {
                int r = candidateRadii[i];
                offsetLookup[i] = new List<(int dx, int dy)>(angleCount);
                for (int a = 0; a < angleCount; a++)
                {
                    double angle = a * angleStep * Math.PI / 180.0;
                    int dx = (int)Math.Round(r * Math.Cos(angle));
                    int dy = (int)Math.Round(r * Math.Sin(angle));
                    offsetLookup[i].Add((dx, dy));
                }
            }

            // Copy pixel data (continuous, row-major, 1 byte per pixel).
            byte[] pixelBytes = new byte[width * height];
            if (pixelBytes.Length == 0) return [];
            for (int y = 0; y < height; y++)
            {
                System.Runtime.InteropServices.Marshal.Copy(gray.Ptr(y), pixelBytes, y * width, width);
            }

            // Allocate a 1D accumulator array for (x, y, radius) combinations.
            int[] accumulator = new int[width * height * radiusCount];
            int stride = width;
            int effectiveThreshold = (int)Math.Round(voteThresholdFraction * angleCount);

            // Voting: for every edge pixel (pixel intensity > edgeThreshold), add votes for candidate circle centers.
            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    int pixelIndex = y * stride + x;
                    if (pixelBytes[pixelIndex] > edgeThreshold)
                    {
                        for (int ri = 0; ri < radiusCount; ri++)
                        {
                            foreach (var (dx, dy) in offsetLookup[ri])
                            {
                                int aX = x - dx;
                                int aY = y - dy;
                                if (aX >= 0 && aX < width && aY >= 0 && aY < height)
                                {
                                    int index = (aX + aY * width) + ri * width * height;
                                    Interlocked.Increment(ref accumulator[index]);
                                }
                            }
                        }
                    }
                }
            });

            // Collect candidate circles.
            int imageCenterX = width / 2;
            int imageCenterY = height / 2;
            List<Circle> candidateCircles = [];
            for (int ri = 0; ri < radiusCount; ri++)
            {
                int r = candidateRadii[ri];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = (x + y * width) + ri * width * height;
                        if (accumulator[index] >= effectiveThreshold)
                        {
                            int dx = x - imageCenterX;
                            int dy = y - imageCenterY;
                            if (Math.Sqrt(dx * dx + dy * dy) <= centerProximityThreshold)
                            {
                                candidateCircles.Add(new Circle
                                {
                                    X = x,
                                    Y = y,
                                    Radius = r,
                                    Votes = accumulator[index]
                                });
                            }
                        }
                    }
                }
            }

            // Non-maximum suppression: merge duplicates (detections with centers and radii differing less than radiusStep).
            List<Circle> filteredCircles = [];
            foreach (var circle in candidateCircles.OrderByDescending(c => c.Votes))
            {
                bool duplicateFound = false;
                foreach (var existing in filteredCircles)
                {
                    double centerDist = Math.Sqrt((circle.X - existing.X) * (circle.X - existing.X) +
                                                  (circle.Y - existing.Y) * (circle.Y - existing.Y));
                    if (centerDist < radiusStep && Math.Abs(circle.Radius - existing.Radius) < radiusStep)
                    {
                        if (circle.Votes > existing.Votes)
                        {
                            existing.X = circle.X;
                            existing.Y = circle.Y;
                            existing.Radius = circle.Radius;
                            existing.Votes = circle.Votes;
                        }
                        duplicateFound = true;
                        break;
                    }
                }
                if (!duplicateFound)
                {
                    filteredCircles.Add(circle);
                }
            }

            return filteredCircles;
        }

        public static void DrawCircle(Mat image, int circleX, int circleY, int circleR, Scalar color, int strokeWidth = 1)
        {
            Cv2.Circle(image, new Point(circleX, circleY), circleR, color, strokeWidth, LineTypes.AntiAlias);
        }

        public static AnalysisResult AnalyzeResult(Mat original, List<Circle> circles, Options options)
        {
            Scalar[] palette =
            [
                new(0, 255, 255, 255),
                new(255, 128, 0, 255),
                new(0, 0, 255, 255),
                new(255, 0, 255, 255),
                new(0, 255, 0, 255),
                new(0, 165, 255, 255),
                new(255, 255, 0, 255),
                new(255, 255, 255, 255)
            ];

            // Draw detected circles (BGRA, opaque); color is determined by size rank so it is stable between frames
            int rank = 0;
            foreach (Circle circle in circles.OrderByDescending(c => c.Radius))
            {
                Scalar color = palette[rank++ % palette.Length];
                DrawCircle(original, circle.X, circle.Y, circle.Radius, color);
                DrawCircle(original, circle.X, circle.Y, 2, color);
            }

            return new AnalysisResult
            {
                CircleCount = circles.Count,
                CenterRMSE = CalculateCenterRMSE(circles),
                CenterRMSEPercent = circles.Count == 0 || circles.Max(c => c.Radius) <= 0
                    ? 0
                    : CalculateCenterRMSE(circles) / circles.Max(c => c.Radius) * 100.0
            };
        }

        internal static Mat LoadImage(string fileName)
        {
            return Cv2.ImRead(fileName, ImreadModes.Color);
        }

        /// <summary>
        /// Calculates the RMSE (root-mean-squared error) of circle centers relative to their average center.
        /// This provides a measure of how tightly the detected circle centers cluster around the mean.
        /// RMSE = square root of (1/N multiplied by the sum for i = 1 to N of [ (x_i minus average x) squared plus (y_i minus average y) squared ]).
        /// </summary>
        /// <param name="circles">List of detected circles.</param>
        /// <returns>RMSE value as a double. Returns 0 if the list is empty.</returns>
        public static double CalculateCenterRMSE(this List<Circle> circles)
        {
            if (circles == null || circles.Count == 0)
                return 0;

            // Compute average center coordinates.
            double avgX = circles.Average(c => c.X);
            double avgY = circles.Average(c => c.Y);

            // Compute the sum of squared distances from each circle center to the average center.
            double sumSquares = circles.Sum(c =>
            {
                double dx = c.X - avgX;
                double dy = c.Y - avgY;
                return dx * dx + dy * dy;
            });

            // Return the square root of the mean squared distance.
            return Math.Sqrt(sumSquares / circles.Count);
        }
    }
}
