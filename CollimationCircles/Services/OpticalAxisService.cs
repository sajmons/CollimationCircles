using OpenCvSharp;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearRegression;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CollimationCircles.Services
{
    public class OpticalAxisService
    {
        public class CircularFeature
        {
            public Point2d Center { get; set; }
            public double Radius { get; set; }
            public double Confidence { get; set; }
        }

        public struct ScrewAdjustment
        {
            public int Id { get; set; }
            public string Direction { get; set; } // "CW", "CCW", "Fixed"
            public double Magnitude { get; set; }
            public double Turns { get; set; }
        }

        public class CollimationResult
        {
            public required CircularFeature SecondaryMirror { get; set; }
            public required CircularFeature PrimaryMirror { get; set; }
            public required CircularFeature CenterMark { get; set; }
            public double AlignmentErrorArcsec { get; set; }
        }

        /// <summary>
        /// Robust Circle Fit using RANSAC to handle outliers (spider vanes, clips).
        /// </summary>
        public static CircularFeature? FitCircleRobust(List<Point2d> points, int iterations = 100, double threshold = 2.0)
        {
            if (points.Count < 10) return null;

            CircularFeature? bestModel = null;
            int maxInliers = -1;
            Random rng = new();

            for (int i = 0; i < iterations; i++)
            {
                // Sample 3 random points to define a circle
                var sample = new List<Point2d>();
                for (int j = 0; j < 3; j++) sample.Add(points[rng.Next(points.Count)]);

                var model = FitCircleAlgebraic(sample);
                if (model == null || double.IsNaN(model.Radius)) continue;

                int inliers = 0;
                foreach (var p in points)
                {
                    double dist = Math.Abs(Math.Sqrt(Math.Pow(p.X - model.Center.X, 2) + Math.Pow(p.Y - model.Center.Y, 2)) - model.Radius);
                    if (dist < threshold) inliers++;
                }

                if (inliers > maxInliers)
                {
                    maxInliers = inliers;
                    bestModel = model;
                }
            }

            // Final refit using only inliers
            if (bestModel != null)
            {
                var finalInliers = points.Where(p => 
                    Math.Abs(Math.Sqrt(Math.Pow(p.X - bestModel.Center.X, 2) + Math.Pow(p.Y - bestModel.Center.Y, 2)) - bestModel.Radius) < threshold).ToList();
                return FitCircleAlgebraic(finalInliers);
            }

            return null;
        }

        private static CircularFeature? FitCircleAlgebraic(List<Point2d> pts)
        {
            if (pts.Count < 3) return null;

            double avgX = pts.Average(p => p.X);
            double avgY = pts.Average(p => p.Y);

            var M = Matrix<double>.Build;
            var V = Vector<double>.Build;

            var A = M.Dense(pts.Count, 3);
            var b = V.Dense(pts.Count);

            for (int i = 0; i < pts.Count; i++)
            {
                A[i, 0] = pts[i].X;
                A[i, 1] = pts[i].Y;
                A[i, 2] = 1;
                b[i] = -(pts[i].X * pts[i].X + pts[i].Y * pts[i].Y);
            }

            try 
            {
                var sol = A.Solve(b);
                double centerX = -sol[0] / 2.0;
                double centerY = -sol[1] / 2.0;
                double radiusSq = (sol[0] * sol[0] + sol[1] * sol[1]) / 4.0 - sol[2];
                if (radiusSq < 0) return null;

                return new CircularFeature
                {
                    Center = new Point2d(centerX, centerY),
                    Radius = Math.Sqrt(radiusSq),
                    Confidence = 1.0
                };
            }
            catch { return null; }
        }

        /// <summary>
        /// Refines edges to sub-pixel precision using quadratic interpolation along the gradient direction.
        /// </summary>
        public static List<Point2d> RefineEdgesSubpixel(Mat gray, List<Point2d> coarseEdges)
        {
            List<Point2d> refined = [];
            
            using Mat dx = new();
            using Mat dy = new();
            Cv2.Sobel(gray, dx, MatType.CV_32F, 1, 0);
            Cv2.Sobel(gray, dy, MatType.CV_32F, 0, 1);

            foreach (var pt in coarseEdges)
            {
                int x = (int)Math.Round(pt.X);
                int y = (int)Math.Round(pt.Y);
                if (x < 2 || x >= gray.Width - 2 || y < 2 || y >= gray.Height - 2) continue;

                float vx = dx.At<float>(y, x);
                float vy = dy.At<float>(y, x);
                double mag = Math.Sqrt(vx * vx + vy * vy);
                if (mag < 0.1) continue;

                // Normalize gradient
                double nx = vx / mag;
                double ny = vy / mag;

                // Sample intensities along gradient: f(-1), f(0), f(1)
                // We use simple bilinear interpolation or just pixel neighbors for performance
                double vMinus = GetIntensity(gray, pt.X - nx, pt.Y - ny);
                double vCenter = GetIntensity(gray, pt.X, pt.Y);
                double vPlus = GetIntensity(gray, pt.X + nx, pt.Y + ny);

                // Parabolic interpolation: vertex is at -0.5 * (f(1) - f(-1)) / (f(1) - 2f(0) + f(-1))
                double denom = vPlus - 2 * vCenter + vMinus;
                if (Math.Abs(denom) > 0.001)
                {
                    double offset = -0.5 * (vPlus - vMinus) / denom;
                    if (Math.Abs(offset) < 1.0)
                    {
                        refined.Add(new Point2d(pt.X + offset * nx, pt.Y + offset * ny));
                    }
                }
            }
            return refined;
        }

        private static double GetIntensity(Mat img, double x, double y)
        {
            int ix = (int)Math.Floor(x);
            int iy = (int)Math.Floor(y);
            double fx = x - ix;
            double fy = y - iy;

            // Bilinear interpolation
            double v00 = img.At<byte>(iy, ix);
            double v10 = img.At<byte>(iy, ix + 1);
            double v01 = img.At<byte>(iy + 1, ix);
            double v11 = img.At<byte>(iy + 1, ix + 1);

            return (1 - fx) * (1 - fy) * v00 + fx * (1 - fy) * v10 + (1 - fx) * fy * v01 + fx * fy * v11;
        }

        /// <summary>
        /// Detects the primary mirror center mark (donut/triangle) using Template Matching + Centroiding.
        /// </summary>
        public static CircularFeature? DetectCenterMark(Mat image, Mat template)
        {
            using Mat result = new();
            Cv2.MatchTemplate(image, template, result, TemplateMatchModes.CCoeffNormed);
            Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);

            if (maxVal < 0.7) return null;

            return new CircularFeature
            {
                Center = new Point2d(maxLoc.X + template.Width / 2.0, maxLoc.Y + template.Height / 2.0),
                Radius = template.Width / 2.0,
                Confidence = maxVal
            };
        }

        /// <summary>
        /// Analyzes a defocused star to calculate Coma and Astigmatism using Zernike moments.
        /// </summary>
        public static Dictionary<string, double>? AnalyzeStarWavefront(Mat starImg)
        {
            // 1. Find Centroid (L10, L01)
            var m = Cv2.Moments(starImg, true);
            double cX = m.M10 / m.M00;
            double cY = m.M01 / m.M00;

            // 2. Identify Geometric Center (using Ocular shadow if possible)
            // For now, assume highest intensity region vs geometric center of the Airy disc
            var disc = FitCircleRobust(FindEdgePoints(starImg));
            if (disc == null) return null;

            double dx = cX - disc.Center.X;
            double dy = cY - disc.Center.Y;

            // Coma is proportional to the offset between Intensity Centroid and Geometric Center
            double comaMagnitude = Math.Sqrt(dx * dx + dy * dy) / disc.Radius;
            double comaAngle = Math.Atan2(dy, dx) * 180.0 / Math.PI;

            return new Dictionary<string, double>
            {
                { "ComaMagnitude", comaMagnitude },
                { "ComaAngle", comaAngle },
                { "CentroidX", cX },
                { "CentroidY", cY },
                { "GeometricX", disc.Center.X },
                { "GeometricY", disc.Center.Y }
            };
        }

        public static List<ScrewAdjustment> CalculateScrewAdjustments(double comaMagnitude, double comaAngleDegrees, double rotationAngleDegrees, int count, double sensitivity = 2.0)
        {
            var adjustments = new List<ScrewAdjustment>();
            if (comaMagnitude < 0.005) // Aligned threshold
            {
                for (int i = 1; i <= count; i++) 
                    adjustments.Add(new ScrewAdjustment { Id = i, Direction = "Fixed", Magnitude = 0, Turns = 0 });
                return adjustments;
            }

            double angleStep = 360.0 / count;

            for (int i = 0; i < count; i++)
            {
                // Screw 0 starts at 90 + RotationAngle (down)
                double screwAngleDeg = 90.0 + rotationAngleDegrees + (i * angleStep);
                double screwAngleRad = screwAngleDeg * Math.PI / 180.0;
                double comaAngleRad = comaAngleDegrees * Math.PI / 180.0;

                // Project coma vector onto screw vector
                double projection = comaMagnitude * Math.Cos(comaAngleRad - screwAngleRad);

                string dir = projection > 0 ? "CW" : "CCW";
                adjustments.Add(new ScrewAdjustment 
                { 
                    Id = i + 1, 
                    Direction = dir, 
                    Magnitude = Math.Abs(projection),
                    Turns = Math.Abs(projection) * sensitivity // Heuristic turn amount
                });
            }

            return adjustments;
        }

        private static List<Point2d> FindEdgePoints(Mat img)
        {
            using Mat edges = new();
            Cv2.Canny(img, edges, 50, 150);
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

        /// <summary>
        /// Generates a Zernike Polynomial value at (rho, theta).
        /// </summary>
        public static double Zernike(int n, int m, double rho, double theta)
        {
            if (rho > 1.0) return 0;
            double r = Radial(n, Math.Abs(m), rho);
            return m >= 0 ? r * Math.Cos(m * theta) : r * Math.Sin(-m * theta);
        }

        private static double Radial(int n, int m, double rho)
        {
            if ((n - m) % 2 != 0) return 0;
            double res = 0;
            for (int k = 0; k <= (n - m) / 2; k++)
            {
                double num = Math.Pow(-1, k) * Factorial(n - k);
                double den = Factorial(k) * Factorial((n + m) / 2 - k) * Factorial((n - m) / 2 - k);
                res += (num / den) * Math.Pow(rho, n - 2 * k);
            }
            return res;
        }

        private static double Factorial(int n)
        {
            double res = 1;
            for (int i = 2; i <= n; i++) res *= i;
            return res;
        }
    }
}
