using ImageMagick;
using ImageMagick.Drawing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    internal static class ImageAnalysisService
    {
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
            public double MinConturArea { get; internal set; } = 2000;
            public int MinCircleRadius { get; internal set; } = 50;
            public int MaxCircleRadius { get; internal set; } = 2000;
            public int MinCirclesDetected { get; set; } = 2;
            public double OffsetLimit { get; set; } = 5.0;
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
            public double? Offset { get; set; } = null;
            public string? Error { get; set; } = null;
            public int? CircleCount { get; set; } = null;
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
                image.Edge(10);
                FilterComplited?.Invoke(null, new FilterComplitedEventArgs(nameof(image.Edge), image.ToByteArray()));
            }

            if (options.DoCrop && image.BoundingBox is not null)
            {
                image.Crop(image.BoundingBox);
                FilterComplited?.Invoke(null, new FilterComplitedEventArgs(nameof(image.Crop), image.ToByteArray()));
            }
        }

        /// <summary>
        /// Detect circles in an 8-bit grayscale image using the Hough transform.
        /// Only circles with centers near the image center (within centerProximityThreshold)
        /// and whose accumulator value exceeds a given fraction (0 to 1) of the maximum possible votes are returned.
        /// A post-processing step then merges detections that are too similar (i.e. with center and radius differences less than radiusStep).
        /// </summary>
        /// <param name="edgeImage">
        /// A MagickImage representing an 8bpp grayscale edge image. Pixels above 128 are treated as edges.
        /// </param>
        /// <param name="minRadius">Minimum circle radius to search.</param>
        /// <param name="maxRadius">Maximum circle radius to search.</param>
        /// <param name="centerProximityThreshold">
        /// Maximum Euclidean distance from the image center that a circle center can have to be accepted.
        /// </param>
        /// <param name="voteThresholdFraction">
        /// Fraction (from 0 to 1) representing the minimum percentage of the maximum votes (i.e. the number of angles)
        /// required for a candidate circle to be accepted.
        /// </param>
        /// <param name="angleStep">
        /// Angular discretization in degrees. Smaller values improve accuracy at the cost of speed.
        /// </param>
        /// <param name="radiusStep">
        /// Step (in pixels) for increasing the candidate radius. For example, 1 tests every radius while 2 tests every other radius.
        /// </param>
        /// <returns>List of filtered circles detected that meet the criteria.</returns>
        public static List<Circle> DetectCircles(MagickImage edgeImage, int minRadius, int maxRadius,
                                                   int centerProximityThreshold, double voteThresholdFraction,
                                                   int angleStep = 5, int radiusStep = 1)
        {
            // Ensure the image is in 8-bit grayscale. If not, convert.
            if (edgeImage.ColorSpace != ColorSpace.Gray || edgeImage.Depth != 8)
            {
                edgeImage.ColorSpace = ColorSpace.Gray;
                edgeImage.Depth = 8;
            }

            int width = (int)edgeImage.Width;
            int height = (int)edgeImage.Height;
            // Adjust the count of candidate radii based on the radiusStep.
            int radiusCount = ((maxRadius - minRadius) / radiusStep) + 1;

            // Extract pixel data using the intensity channel ("I")
            byte[] pixelBytes = edgeImage.GetPixels().ToByteArray("I");

            // Allocate a 1D accumulator array for (x, y, radius) combinations.
            int[] accumulator = new int[width * height * radiusCount];

            // Precompute sine and cosine tables for the discretized angles.
            int angleCount = 360 / angleStep;
            double[] sinTable = new double[angleCount];
            double[] cosTable = new double[angleCount];
            for (int a = 0; a < angleCount; a++)
            {
                double angle = a * angleStep * Math.PI / 180.0;
                sinTable[a] = Math.Sin(angle);
                cosTable[a] = Math.Cos(angle);
            }

            // The stride for our pixel array is the image width.
            int stride = width;

            // Each perfect circle could receive at most one vote per discrete angle,
            // so the maximum votes is angleCount. Compute the effective threshold.
            int effectiveThreshold = (int)Math.Round(voteThresholdFraction * angleCount);

            // Voting: Process the image rows in parallel.
            Parallel.For(0, height, y =>
            {
                for (int x = 0; x < width; x++)
                {
                    int pixelIndex = y * stride + x;
                    // Treat pixel as an edge if its intensity > 128.
                    if (pixelBytes[pixelIndex] > 128)
                    {
                        // Iterate over candidate radii using the specified radiusStep.
                        for (int r = minRadius; r <= maxRadius; r += radiusStep)
                        {
                            int rIndex = (r - minRadius) / radiusStep;
                            for (int a = 0; a < angleCount; a++)
                            {
                                int aX = (int)Math.Round(x - r * cosTable[a]);
                                int aY = (int)Math.Round(y - r * sinTable[a]);

                                if (aX >= 0 && aX < width && aY >= 0 && aY < height)
                                {
                                    int index = (aX + aY * width) + rIndex * width * height;
                                    Interlocked.Increment(ref accumulator[index]);
                                }
                            }
                        }
                    }
                }
            });

            // Define the image center.
            int imageCenterX = width / 2;
            int imageCenterY = height / 2;
            List<Circle> candidateCircles = new List<Circle>();

            // Scan the accumulator for peaks exceeding the effective threshold.
            for (int r = minRadius; r <= maxRadius; r += radiusStep)
            {
                int rIndex = (r - minRadius) / radiusStep;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = (x + y * width) + rIndex * width * height;
                        if (accumulator[index] >= effectiveThreshold)
                        {
                            // Filter: only accept circles whose centers are near the image center.
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

            // Post-process: filter duplicate detections.
            // Two circles are considered duplicates if their centers are close (within radiusStep)
            // and their radii differ by less than radiusStep.
            List<Circle> filteredCircles = new List<Circle>();
            foreach (var circle in candidateCircles)
            {
                bool duplicateFound = false;
                foreach (var existing in filteredCircles)
                {
                    double centerDist = Math.Sqrt((circle.X - existing.X) * (circle.X - existing.X) +
                                                  (circle.Y - existing.Y) * (circle.Y - existing.Y));
                    if (centerDist < radiusStep && Math.Abs(circle.Radius - existing.Radius) < radiusStep)
                    {
                        // If duplicate is found, keep the circle with higher votes.
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

        public static AnalysisResult AnalyzeResult(MagickImage image, List<Circle> circles, Options options)
        {
            string? error = null;
            double? offset = null;

            Random rng = new(12345);            

            // Draw detected circles on the image
            foreach (Circle circle in circles)
            {
                MagickColor color = new((byte)rng.Next(255), (byte)rng.Next(255), (byte)rng.Next(255));
                DrawCircle(image, circle.X, circle.Y, circle.Radius, color);
                DrawCircle(image, circle.X, circle.Y, 2, color);
            }

            if (circles.Count == 0)
            {
                error = "No circles detected in image.";
            }
            else
            {
                // Compute average center of circles
                var avgCenter = new PointD(
                    circles.Average(c => c.X),
                    circles.Average(c => c.Y)
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

        internal static MagickImage LoadImage(string fileName)
        {
            return new MagickImage(fileName);
        }
    }
}
