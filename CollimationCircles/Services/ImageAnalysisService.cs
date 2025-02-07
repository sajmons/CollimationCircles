using ImageMagick;
using ImageMagick.Drawing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    internal static class ImageAnalysisService
    {
        public struct Circle
        {
            public int CenterX;
            public int CenterY;
            public int Radius;
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
            public string ImagesPath { get; set; } = ".\\";
            public bool SaveImages { get; set; } = false;
            public bool ShowEachImage { get; set; } = false;
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
                if (options.SaveImages) image.Write("1_grayscale.jpg");
            }

            if (options.DoNormalize)
            {
                // Apply preprocessing filters
                image.Normalize();
                if (options.SaveImages) image.Write("2_normalize.jpg");
            }

            if (options.DoNormalize)
            {
                image.GaussianBlur(
                    options.BlurOptions.Radius,
                    options.BlurOptions.Sigma,
                    options.BlurOptions.Channels);
                if (options.SaveImages) image.Write("3_gausian_blur.jpg");
            }

            if (options.DoThreshold)
            {
                image.Threshold(
                    options.AdaptiveThresholdOptions.Percentage,
                    options.AdaptiveThresholdOptions.Chanels);
                if (options.SaveImages) image.Write("4_threshold.jpg");
            }

            if (options.DoErode)
            {
                image.Morphology(options.ErodeSettings);
                if (options.SaveImages) image.Write("5_erode.jpg");
            }

            if (options.DoDilate)
            {
                image.Morphology(options.DilateSettings);
                if (options.SaveImages) image.Write("6_dilate.jpg");
            }

            if (options.DoEdge)
            {
                image.Edge(10);
                if (options.SaveImages) image.Write("7_edge.jpg");
            }

            if (options.DoCrop && image.BoundingBox is not null)
            {
                image.Crop(image.BoundingBox);
                if (options.SaveImages) image.Write("8_crop.jpg");
            }
        }

        /// <summary>
        /// Detect circles in an image using a Hough Transform.
        /// </summary>
        /// <param name="image">Input image (MagickImage).</param>
        /// <param name="minRadius">Minimum circle radius to detect.</param>
        /// <param name="maxRadius">Maximum circle radius to detect.</param>
        /// <param name="edgeThreshold">
        /// Intensity threshold for an edge pixel (0-255). Pixels with intensity equal to or above this value are considered edges.
        /// </param>
        /// <param name="voteThresholdFactor">
        /// Fraction of the total angular steps required as votes (e.g. 0.5 means at least half of the steps must vote).
        /// </param>
        /// <param name="angleStep">
        /// Step size in degrees for iterating the circle perimeter (e.g. 5 means 360/5 = 72 steps per circle).
        /// </param>
        /// <param name="centerTolerance">
        /// Maximum distance (in pixels) from the image center for a circle to be accepted.
        /// </param>
        /// <param name="pixelStep">
        /// Step size for iterating over image pixels in both x and y directions (1 processes every pixel, 2 processes every other pixel, etc.).
        /// </param>
        /// <returns>List of detected circles (with centers near the image center).</returns>
        public static List<Circle> DetectCircles(
            MagickImage image,
            int minRadius,
            int maxRadius,
            byte edgeThreshold,
            double voteThresholdFactor,
            int angleStep,
            int centerTolerance,
            int pixelStep)
        {
            // Work on a clone so the original image is not modified.
            var procImage = image.Clone();

            // Convert image to grayscale.
            procImage.ColorSpace = ColorSpace.Gray;

            int width = (int)procImage.Width;
            int height = (int)procImage.Height;

            // Extract grayscale pixel data ("I" channel for intensity).
            // Use StorageType.Char for 8-bit pixel data.
            byte[]? pixelData = procImage.GetPixels().ToByteArray("RGB");

            if (pixelData is null) return [];

            // Precompute the center of the image.
            int imageCenterX = width / 2;
            int imageCenterY = height / 2;

            // List to hold detected circles (thread-safe additions later).
            List<Circle> detectedCircles = [];
            object lockObj = new();

            // Process each candidate radius in parallel.
            Parallel.For(minRadius, maxRadius + 1, r =>
            {
                // Create a 2D accumulator array for potential circle centers for current radius.
                int[,] accumulator = new int[width, height];

                // Determine the number of angular steps.
                int steps = 360 / angleStep;
                // The number of votes required to consider a candidate as a circle.
                double voteThreshold = steps * voteThresholdFactor;

                // Precompute sine and cosine for each angle step.
                double[] cosTable = new double[steps];
                double[] sinTable = new double[steps];
                double angleIncrement = 2 * Math.PI / steps;
                for (int i = 0; i < steps; i++)
                {
                    double theta = i * angleIncrement;
                    cosTable[i] = Math.Cos(theta);
                    sinTable[i] = Math.Sin(theta);
                }

                // Iterate over image pixels using the pixelStep parameter.
                for (int y = 0; y < height; y += pixelStep)
                {
                    for (int x = 0; x < width; x += pixelStep)
                    {
                        // Calculate index for 1D pixelData array.
                        int index = y * width + x;
                        byte intensity = pixelData[index];

                        // Consider this pixel an edge pixel if intensity meets threshold.
                        if (intensity >= edgeThreshold)
                        {
                            // Vote for each candidate center along the circle perimeter.
                            for (int i = 0; i < steps; i++)
                            {
                                int a = (int)Math.Round(x - r * cosTable[i]);
                                int b = (int)Math.Round(y - r * sinTable[i]);

                                // Only count votes within image bounds.
                                if (a >= 0 && a < width && b >= 0 && b < height)
                                {
                                    accumulator[a, b]++;
                                }
                            }
                        }
                    }
                }

                // Now, search for accumulator cells that have votes above the threshold.
                for (int cy = 0; cy < height; cy++)
                {
                    for (int cx = 0; cx < width; cx++)
                    {
                        if (accumulator[cx, cy] >= voteThreshold)
                        {
                            // Filter: only accept circles whose centers lie near the image center.
                            if (Math.Abs(cx - imageCenterX) <= centerTolerance &&
                                Math.Abs(cy - imageCenterY) <= centerTolerance)
                            {
                                var circle = new Circle { CenterX = cx, CenterY = cy, Radius = r };
                                lock (lockObj)
                                {
                                    detectedCircles.Add(circle);
                                }
                            }
                        }
                    }
                }
            });

            return detectedCircles;
        }

        public static void DrawText(MagickImage image, string text, double x, double y, MagickColor color)
        {
            new Drawables()
            // // Draw text on the image
            .FontPointSize(36)
            .Font("Impact", FontStyleType.Italic, FontWeight.Bold, FontStretch.ExtraExpanded)
            .StrokeColor(color)
            .FillColor(color)
            .Text(x, y, text)
            .Draw(image);
        }

        public static void DrawCircle(MagickImage image, double x, double y, double radius, MagickColor circleColor)
        {
            // Create a drawing object
            var drawables = new Drawables()
                .StrokeColor(circleColor)
                .StrokeWidth(2)
                .FillColor(MagickColors.None) // No fill
                .Circle(
                    x, y,
                    radius, radius
                );

            // Draw the circle on the image
            image.Draw(drawables);
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
                DrawCircle(image, circle.CenterX, circle.CenterY, circle.Radius, color);
                DrawCircle(image, circle.CenterX, circle.CenterY, 2, color);
            }

            if (circles.Count == 0)
            {
                error = "No circles detected in image.";
            }
            else
            {
                // Compute average center of circles
                var avgCenter = new PointD(
                    circles.Average(c => c.CenterX),
                    circles.Average(c => c.CenterY)
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
