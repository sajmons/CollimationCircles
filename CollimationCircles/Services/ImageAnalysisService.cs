using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CollimationCircles.Services
{
    internal class ImageAnalysisService
    {
        public static byte[] FileToByteArray(string fileName)
        {
            byte[] buff = [];
            FileStream fs = new(fileName, FileMode.Open, FileAccess.Read);
            BinaryReader br = new(fs);
            long numBytes = new FileInfo(fileName).Length;
            buff = br.ReadBytes((int)numBytes);
            return buff;
        }

        /// <summary>
        /// Compute Euclidean distance (offset) between the average circle center and the image center
        /// </summary>
        /// <param name="sourceImageFile">Input image</param>
        /// <param name="saveImages">Save images for debugging</param>
        /// <param name="showFinalImage">Show final image</param>
        /// <returns></returns>
        public static double AnalyzeStarTestImage(string sourceImageFile, bool saveImages = false, bool showFinalImage = false)
        {
            Mat image = Cv2.ImRead(sourceImageFile, ImreadModes.Grayscale);

            return AnalyzeStarTestImageInternal(image, saveImages, showFinalImage);
        }

        /// <summary>
        /// Compute Euclidean distance (offset) between the average circle center and the image center
        /// </summary>
        /// <param name="sourceImageBytes">Input image byte array</param>
        /// <param name="saveImages">Save images for debugging</param>
        /// <param name="showFinalImage">Show final image</param>
        /// <returns></returns>
        public static double AnalyzeStarTestImage(byte[] sourceImageBytes, bool saveImages = false, bool showFinalImage = false)
        {
            Mat image = Mat.FromImageData(sourceImageBytes, ImreadModes.Grayscale);

            return AnalyzeStarTestImageInternal(image, saveImages, showFinalImage);
        }

        /// <summary>
        /// Compute Euclidean distance (offset) between the average circle center and the image center
        /// </summary>
        /// <param name="image">Input image</param>
        /// <param name="saveImages">Save images for debugging</param>
        /// <param name="showFinalImage">Show final image</param>
        /// <returns></returns>
        private static double AnalyzeStarTestImageInternal(Mat image, bool saveImages = true, bool showFinalImage = false)
        {
            try
            {
                string path = ".\\";

                // Normalize brightness and contrast
                Cv2.Normalize(image, image, 0, 255, NormTypes.MinMax);
                if (saveImages) image.SaveImage($"{path}0_Normalize.jpg");

                // Apply Gaussian blur                
                Cv2.MedianBlur(image, image, 11);
                if (saveImages) image.SaveImage($"{path}1_MedianBlur.jpg");

                // Apply Adaptive threshold
                Cv2.AdaptiveThreshold(image, image, 20, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 51, 0);
                if (saveImages) image.SaveImage($"{path}2_AdaptiveThreshold.jpg");

                // Morph open 
                Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
                Cv2.MorphologyEx(image, image, MorphTypes.Open, kernel, iterations: 3);
                if (saveImages) image.SaveImage($"{path}3_MorphologyEx.jpg");

                // Apply Canny edge detector
                //Mat edges = new();
                Cv2.Canny(image, image, 0, 50);
                if (saveImages) image.SaveImage($"{path}4_Canny.jpg");

                //// Find contours
                Cv2.FindContours(image, out Point[][] contours, out HierarchyIndex[] hierarchy
                    , RetrievalModes.List, ContourApproximationModes.ApproxSimple);

                List<CircleSegment> conCircles = [];

                // Fit circles to contours
                foreach (var contour in contours)
                {
                    double area = Cv2.ContourArea(contour);
                    if (area > 2000 && area < image.Width * image.Height) // Filter based on area
                    {
                        Cv2.MinEnclosingCircle(contour, out Point2f center, out float radius);
                        conCircles.Add(new CircleSegment(center, radius));
                        Cv2.Circle(image, (Point)center, (int)radius, Scalar.White, 2);
                    }
                }

                if (saveImages) image.SaveImage($"{path}5_FindContours.jpg");

                // Display the image
                if (showFinalImage) Cv2.ImShow("Image analysis", image);

                // Compute average center of circles
                var avgCenter = new Point2d(
                    conCircles.Average(c => c.Center.X),
                    conCircles.Average(c => c.Center.Y)
                );

                // Compute image center
                Point2d imageCenter = new(image.Width / 2, image.Height / 2);

                // Compute Euclidean distance (offset) between the average circle center and the image center
                double offset = Math.Sqrt(
                    Math.Pow(avgCenter.X - imageCenter.X, 2) +
                    Math.Pow(avgCenter.Y - imageCenter.Y, 2)
                );

                return offset;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            return -1;
        }
    }
}
