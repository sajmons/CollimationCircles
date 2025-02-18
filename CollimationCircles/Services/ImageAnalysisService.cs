using ImageMagick;
using ImageMagick.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    internal static class ImageAnalysisService
    {
        public class DetectionAccuracy
        {
            public static DetectionParameters Maximum = new()
            {                
                CenterProximityThreshold = 5,
                VoteThresholdFraction = 1,
                AngleStep = 1,
                RadiusStep = 1,
                EdgeThreshold = 80
            };
            public static DetectionParameters High = new ()
            {
                CenterProximityThreshold = 5,
                VoteThresholdFraction = 0.85,
                AngleStep = 2,
                RadiusStep = 2,
                EdgeThreshold = 128
            };
            public static DetectionParameters Medium = new()
            {
                CenterProximityThreshold = 5,
                VoteThresholdFraction = 0.8,
                AngleStep = 3,
                RadiusStep = 3,
                EdgeThreshold = 150
            };
            public static DetectionParameters Low = new()
            {
                CenterProximityThreshold = 5,
                VoteThresholdFraction = 0.6,
                AngleStep = 5,
                RadiusStep = 5,
                EdgeThreshold = 100
            };
        }

        public class DetectionParameters
        {            
            public int CenterProximityThreshold;
            public double VoteThresholdFraction;
            public int AngleStep = 5;
            public int RadiusStep = 1;
            public int EdgeThreshold = 128;
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
            public MorphologySettings ErodeSettings { get; set; } = new()
            {
                Method = MorphologyMethod.Erode,
                Iterations = 1,
                Kernel = Kernel.Disk
            };
            public MorphologySettings DilateSettings { get; set; } = new()
            {
                Method = MorphologyMethod.Dilate,
                Iterations = 1,
                Kernel = Kernel.Disk
            };
            public EdgeOptions EdgeSettings { get; set; } = new();
            public IMagickGeometry? BoundingBox { get; internal set; }
        }

        public class EdgeOptions
        {
            public double Radius { get; set; } = 10;
        }

        public class BlurOptions
        {
            public int Radius { get; set; } = 1;
            public int Sigma { get; set; } = 1;
            public Channels Channels { get; internal set; } = Channels.Gray;
        }

        public class ThresholdOptions
        {
            public Percentage Percentage { get; internal set; } = new Percentage(20);
            public Channels Chanels { get; internal set; } = Channels.Gray;
        }

        public class AnalysisResult
        {
            public int? CircleCount { get; set; } = null;
            public double CenterRMSE { get; set; } = 0.0;
        }

        public static void ProcessImage(MagickImage image, Options options)
        {
            if (options.DoGrayscale)
            {
                // Convert to grayscale directly
                image.Grayscale(PixelIntensityMethod.Average);
                FilterComplited?.Invoke(null, new FilterComplitedEventArgs(nameof(image.Grayscale), image.ToByteArray()));
            }

            if (options.DoNormalize)
            {
                // Apply preprocessing filters
                image.Normalize();
                FilterComplited?.Invoke(null, new FilterComplitedEventArgs(nameof(image.Normalize), image.ToByteArray()));
            }

            if (options.DoNormalize)
            {
                image.GaussianBlur(
                    options.BlurOptions.Radius,
                    options.BlurOptions.Sigma,
                    options.BlurOptions.Channels);
                FilterComplited?.Invoke(null, new FilterComplitedEventArgs(nameof(image.GaussianBlur), image.ToByteArray()));
            }

            if (options.DoThreshold)
            {
                image.Threshold(
                    options.AdaptiveThresholdOptions.Percentage,
                    options.AdaptiveThresholdOptions.Chanels);
                FilterComplited?.Invoke(null, new FilterComplitedEventArgs(nameof(image.Threshold), image.ToByteArray()));
            }

            if (options.DoErode)
            {
                image.Morphology(options.ErodeSettings);
                FilterComplited?.Invoke(null, new FilterComplitedEventArgs(nameof(image.Morphology) + "/Erode", image.ToByteArray()));
            }

            if (options.DoDilate)
            {
                image.Morphology(options.DilateSettings);
                FilterComplited?.Invoke(null, new FilterComplitedEventArgs(nameof(image.Morphology) + "/Dilate", image.ToByteArray()));
            }

            if (options.DoEdge)
            {
                image.Edge(options.EdgeSettings.Radius);
                FilterComplited?.Invoke(null, new FilterComplitedEventArgs(nameof(image.Edge), image.ToByteArray()));
            }

            if (options.DoCrop && image.BoundingBox is not null)
            {
                options.BoundingBox = image.BoundingBox;
                image.Crop(image.BoundingBox);
                FilterComplited?.Invoke(null, new FilterComplitedEventArgs(nameof(image.Crop), image.ToByteArray()));
            }
        }

        /// <summary>
        /// Detect circles in an 8-bit grayscale image using a Hough transform.
        /// Uses a precomputed offset table (per candidate radius) to optimize inner-loop computations.
        /// Only circles with centers near the image center (within centerProximityThreshold)
        /// and whose accumulator value exceeds a given fraction (0 to 1) of the maximum possible votes are returned.
        /// A non‑maximum suppression step merges nearby duplicate detections.
        /// </summary>
        /// <param name="edgeImage">
        /// A MagickImage representing an 8bpp grayscale edge image. Pixels with intensity above edgeThreshold are treated as edges.
        /// </param>
        /// <param name="minRadius">Minimum circle radius to search.</param>
        /// <param name="maxRadius">Maximum circle radius to search.</param>
        /// <param name="centerProximityThreshold">
        /// Maximum Euclidean distance from the image center that a circle center can have to be accepted.
        /// </param>
        /// <param name="voteThresholdFraction">
        /// Fraction (0 to 1) representing the minimum percentage of maximum votes (number of angles) needed for acceptance.
        /// </param>
        /// <param name="angleStep">
        /// Angular discretization in degrees. Smaller values improve accuracy at the cost of speed.
        /// </param>
        /// <param name="radiusStep">
        /// Step (in pixels) for increasing the candidate radius. For example, 1 tests every radius; 2 tests every other radius.
        /// </param>
        /// <param name="edgeThreshold">
        /// Minimum pixel intensity to be considered an edge (0-255). Default is 128.
        /// </param>
        /// <returns>A list of filtered circles detected that meet the criteria.</returns>
        public static List<Circle> DetectCircles(MagickImage edgeImage, int minRadius, int maxRadius, DetectionParameters detectionParameters)
        {
            // Ensure the image is 8-bit grayscale.
            if (edgeImage.ColorSpace != ColorSpace.Gray || edgeImage.Depth != 8)
            {
                edgeImage.ColorSpace = ColorSpace.Gray;
                edgeImage.Depth = 8;
            }

            int radiusStep = detectionParameters.RadiusStep;
            int angleStep = detectionParameters.AngleStep;
            double voteThresholdFraction = detectionParameters.VoteThresholdFraction;
            int edgeThreshold = detectionParameters.EdgeThreshold;
            int centerProximityThreshold = detectionParameters.CenterProximityThreshold;

            int width = (int)edgeImage.Width;
            int height = (int)edgeImage.Height;
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

            // Get pixel data from the edge image using the intensity channel ("I").
            byte[]? pixelBytes = edgeImage.GetPixels().ToByteArray("I");

            if (pixelBytes?.Length == 0) return [];

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
                    if (pixelBytes?[pixelIndex] > edgeThreshold)
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
            foreach (var circle in candidateCircles)
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

        public static void DrawCircle(MagickImage image, int circleX, int circleY, int circleR, MagickColor color, int strokeWidth = 1)
        {
            var drawables = new Drawables(); // Use 'using' for proper disposal
            {
                double x = circleX;
                double y = circleY;
                double radius = circleR;

                drawables.StrokeColor(color)
                         .StrokeWidth(strokeWidth)
                         .FillColor(MagickColors.Transparent)
                         .Arc(x - radius, y - radius, x + radius, y + radius, 0, 360); // Full arc = circle

                image.Draw(drawables); // Apply the drawables to the image
            }
        }

        public static AnalysisResult AnalyzeResult(MagickImage original, List<Circle> circles, Options options)
        {
            Random rng = new();

            if (options.DoCrop && options.BoundingBox is not null)
            {
                original.Crop(options.BoundingBox);
            }

            // Draw detected circles on the image
            foreach (Circle circle in circles)
            {
                MagickColor color = new((byte)rng.Next(255), (byte)rng.Next(255), (byte)rng.Next(255));
                DrawCircle(original, circle.X, circle.Y, circle.Radius, color);
                DrawCircle(original, circle.X, circle.Y, 2, color);
            }

            return new AnalysisResult
            {
                CircleCount = circles.Count,
                CenterRMSE = CalculateCenterRMSE(circles)
            };
        }

        internal static MagickImage LoadImage(string fileName)
        {
            return new MagickImage(fileName);
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
