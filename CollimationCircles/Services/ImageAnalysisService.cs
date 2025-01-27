using OpenCvSharp;
using System;
using System.Linq;

namespace CollimationCircles.Services
{
    internal class ImageAnalysisService
    {
        public void DetectingCollimationErrors()
        {
            try
            {
                string path = "D:\\Projekti\\Sajmons\\CollimationCircles\\Documentation\\RMS\\";

                // Load the image
                Mat image = Cv2.ImRead($"{path}defocused_star_1.jpg", ImreadModes.Grayscale);

                // Preprocess the image
                // Normalize brightness and contrast
                Cv2.Normalize(image, image, 0, 255, NormTypes.MinMax);

                image.SaveImage($"{path}normalized.jpg");

                // Apply Gaussian blur
                Mat blurred = new Mat();
                Cv2.GaussianBlur(image, blurred, new Size(5, 5), 0);

                blurred.SaveImage($"{path}blurred.jpg");

                // Apply Canny edge detector
                Mat edges = new Mat();
                Cv2.Canny(blurred, edges, threshold1: 50, threshold2: 150);

                edges.SaveImage($"{path}canny_edges.jpg");

                // Threshold the image
                Mat thresholded = new Mat();
                Cv2.Threshold(edges, thresholded, 100, 255, ThresholdTypes.Binary);

                thresholded.SaveImage($"{path}treshold.jpg");

                // Find contours
                Cv2.FindContours(thresholded, out Point[][] contours, out HierarchyIndex[] hierarchy, RetrievalModes.List, ContourApproximationModes.ApproxSimple);

                // Fit circles to contours
                foreach (var contour in contours)
                {
                    double area = Cv2.ContourArea(contour);
                    if (area > 10) // Filter based on area
                    {
                        Cv2.MinEnclosingCircle(contour, out Point2f center, out float radius);
                        Cv2.Circle(image, (Point)center, (int)radius, Scalar.Red, 2);
                    }
                }

                image.SaveImage($"{path}contours.jpg");


                // Detect difraction rings
                // Detect circles using Hough Transform
                CircleSegment[] circles = Cv2.HoughCircles(thresholded,
                    HoughModes.Gradient,
                    dp: 1,
                    minDist: 20,
                    param1: 100,
                    param2: 30,
                    minRadius: 10,
                    maxRadius: 100);

                // Draw the detected circles
                foreach (var circle in circles)
                {
                    Cv2.Circle(image, (int)circle.Center.X, (int)circle.Center.Y, (int)circle.Radius, Scalar.Red, 2);
                }


                // Analyze the simetry            
                // Compute average center of circles
                var avgCenter = new Point2d(
                    circles.Average(c => c.Center.X),
                    circles.Average(c => c.Center.Y)
                );

                // Compute image center
                Point2d imageCenter = new Point2d(image.Width / 2, image.Height / 2);

                // Compute Euclidean distance (offset) between the average circle center and the image center
                double offset = Math.Sqrt(
                    Math.Pow(avgCenter.X - imageCenter.X, 2) +
                    Math.Pow(avgCenter.Y - imageCenter.Y, 2)
                );

                // Check for concentricity
                double maxRadiusDeviation = circles.Max(c => Math.Abs(c.Radius - circles.Average(c => c.Radius)));

                // Print results
                Console.WriteLine($"Offset from optical axis: {offset}px");
                Console.WriteLine($"Max radius deviation: {maxRadiusDeviation}px");

                if (offset < 5 && maxRadiusDeviation < 3)
                {
                    Console.WriteLine("Telescope is likely well-collimated.");
                }
                else
                {
                    Console.WriteLine("Collimation issues detected.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}
