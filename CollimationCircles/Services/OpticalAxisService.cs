using OpenCvSharp;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearRegression;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CollimationCircles.Services
{
    /// <summary>
    /// Optical axis helpers: defocused star analysis (<see cref="AnalyzeStarWavefront"/>), circle fitting,
    /// center mark detection and screw adjustment calculation (angle based, calibrated and single screw guidance).
    /// The circle detection and RMSE used by the optical tube analysis are in <see cref="ImageAnalysisService"/>.
    /// </summary>
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
        /// Analyzes a defocused star. The outer disc is the largest bright blob; the secondary mirror shadow is the dark hole inside it.
        /// Collimation error is the offset of the shadow from the disc center, relative to the disc radius.
        /// The defocused pattern is point-inverted (rotated 180 degrees) between inside and outside focus, so for inside focus the
        /// offset vector is negated. ComaMagnitude/ComaAngle are therefore always expressed in the same frame (outside focus),
        /// which keeps calibration and screw guidance valid on both sides of focus. CentroidX/Y stay in raw image coordinates.
        /// </summary>
        private const double MinObstructionRatio = 0.1;
        private const double MaxObstructionRatio = 0.6;

        public static Dictionary<string, double>? AnalyzeStarWavefront(Mat starImg, bool insideFocus = false)
        {
            using Mat gray = starImg.Channels() == 1 ? starImg.Clone() : starImg.CvtColor(ColorConversionCodes.BGR2GRAY);
            using Mat binary = new();
            Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);

            Cv2.FindContours(binary, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            if (contours.Length == 0) return null;

            var outer = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
            double area = Cv2.ContourArea(outer);
            if (area < 100) return null;

            using Mat mask = Mat.Zeros(gray.Size(), MatType.CV_8UC1);
            Cv2.DrawContours(mask, [outer], -1, Scalar.White, -1);

            var outerMoments = Cv2.Moments(mask, true);
            if (outerMoments.M00 <= 0) return null;
            double oX = outerMoments.M10 / outerMoments.M00;
            double oY = outerMoments.M01 / outerMoments.M00;
            double radius = Math.Sqrt(area / Math.PI);

            // Ignore the blurred rim so that only the inner shadow is detected
            using Mat inner = new();
            int k = Math.Max(1, (int)(radius * 0.15));
            using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(2 * k + 1, 2 * k + 1));
            Cv2.Erode(mask, inner, kernel);

            Cv2.MinMaxLoc(gray, out double min, out _, out _, out _, inner);
            double mean = Cv2.Mean(gray, inner).Val0;
            double holeThreshold = min + 0.5 * (mean - min);

            using Mat hole = new();
            Cv2.Threshold(gray, hole, holeThreshold, 255, ThresholdTypes.BinaryInv);
            Cv2.BitwiseAnd(hole, inner, hole);

            Cv2.FindContours(hole, out Point[][] holeContours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            if (holeContours.Length == 0) return null;

            var shadow = holeContours.OrderByDescending(c => Cv2.ContourArea(c)).First();
            if (Cv2.ContourArea(shadow) < 4) return null;

            using Mat shadowMask = Mat.Zeros(gray.Size(), MatType.CV_8UC1);
            Cv2.DrawContours(shadowMask, [shadow], -1, Scalar.White, -1);
            var sm = Cv2.Moments(shadowMask, true);
            if (sm.M00 <= 0) return null;
            double sX = sm.M10 / sm.M00;
            double sY = sm.M01 / sm.M00;

            double shadowRadius = Math.Sqrt(Cv2.ContourArea(shadow) / Math.PI);
            double obstructionRatio = shadowRadius / radius;
            if (obstructionRatio < MinObstructionRatio || obstructionRatio > MaxObstructionRatio) return null;

            double dx = sX - oX;
            double dy = sY - oY;
            double rawMagnitude = Math.Sqrt(dx * dx + dy * dy);

            // Inside focus: the pattern is inverted, so map the offset into the common (outside focus) frame
            double nx = insideFocus ? -dx : dx;
            double ny = insideFocus ? -dy : dy;

            return new Dictionary<string, double>
            {
                { "ComaMagnitude", rawMagnitude / radius },
                { "ComaAngle", Math.Atan2(ny, nx) * 180.0 / Math.PI },
                { "InsideFocus", insideFocus ? 1 : 0 },
                { "OffsetPx", rawMagnitude },
                { "CentroidX", sX },
                { "CentroidY", sY },
                { "GeometricX", oX },
                { "GeometricY", oY },
                { "Radius", radius },
                { "ShadowRadius", Math.Sqrt(Cv2.ContourArea(shadow) / Math.PI) }
            };
        }

        public static List<ScrewAdjustment>? CalculateCalibratedAdjustments(double offsetX, double offsetY, IReadOnlyList<(double X, double Y)> response, double alignedThreshold = 0.001)
        {
            if (response.Count < 2) return null;

            double a = 0, b = 0, c = 0;
            foreach (var (x, y) in response)
            {
                a += x * x;
                b += x * y;
                c += y * y;
            }

            double det = a * c - b * b;
            if (det < 0.01 * (a + c) * (a + c)) return null;

            bool aligned = Math.Sqrt(offsetX * offsetX + offsetY * offsetY) < alignedThreshold;

            double wx = -(c * offsetX - b * offsetY) / det;
            double wy = -(-b * offsetX + a * offsetY) / det;

            var adjustments = new List<ScrewAdjustment>();
            for (int i = 0; i < response.Count; i++)
            {
                double turns = aligned ? 0 : response[i].X * wx + response[i].Y * wy;
                adjustments.Add(new ScrewAdjustment
                {
                    Id = i + 1,
                    Direction = aligned ? "Fixed" : (turns > 0 ? "CW" : "CCW"),
                    Magnitude = Math.Abs(turns),
                    Turns = Math.Abs(turns)
                });
            }

            return adjustments;
        }

        /// <summary>
        /// Picks the single screw whose turn reduces the offset the most and returns the turns for it only;
        /// all other screws are reported as "Fixed".
        /// </summary>
        public static List<ScrewAdjustment>? CalculateSingleScrewAdjustment(double offsetX, double offsetY, IReadOnlyList<(double X, double Y)> response, double alignedThreshold = 0.001)
        {
            if (response.Count < 1) return null;

            bool aligned = Math.Sqrt(offsetX * offsetX + offsetY * offsetY) < alignedThreshold;
            int best = -1;
            double bestGain = 0;
            double bestTurns = 0;
            for (int i = 0; i < response.Count && !aligned; i++)
            {
                double norm = response[i].X * response[i].X + response[i].Y * response[i].Y;
                if (norm <= 1e-12) continue;

                double dot = response[i].X * offsetX + response[i].Y * offsetY;
                double gain = dot * dot / norm;
                if (gain > bestGain)
                {
                    bestGain = gain;
                    bestTurns = -dot / norm;
                    best = i;
                }
            }

            var adjustments = new List<ScrewAdjustment>();
            for (int i = 0; i < response.Count; i++)
            {
                bool active = i == best;
                adjustments.Add(new ScrewAdjustment
                {
                    Id = i + 1,
                    Direction = active ? (bestTurns > 0 ? "CW" : "CCW") : "Fixed",
                    Magnitude = active ? Math.Abs(bestTurns) : 0,
                    Turns = active ? Math.Abs(bestTurns) : 0
                });
            }

            return adjustments;
        }

        /// <summary>
        /// Builds an approximate per-turn response for each screw from the screw geometry, used when no calibration exists.
        /// </summary>
        public static List<(double X, double Y)> BuildEstimatedResponse(double rotationAngleDegrees, int count, double sensitivity = 20.0)
        {
            var response = new List<(double X, double Y)>();
            if (count < 1) return response;

            double angleStep = 360.0 / count;
            for (int i = 0; i < count; i++)
            {
                double rad = (90.0 + rotationAngleDegrees + i * angleStep) * Math.PI / 180.0;
                response.Add((-Math.Cos(rad) / sensitivity, -Math.Sin(rad) / sensitivity));
            }

            return response;
        }

        /// <summary>
        /// Finds the best screw to turn right now: the single screw that reduces the remaining error the most.
        /// Optionally considers pairs of screws (never all screws). All other screws are reported as "Fixed".
        /// </summary>
        public static List<ScrewAdjustment>? CalculateBestSubsetAdjustment(double offsetX, double offsetY, IReadOnlyList<(double X, double Y)> response, double alignedThreshold = 0.001, double minPairImprovement = 0.2, bool allowPairs = false)
        {
            int n = response.Count;
            if (n < 1) return null;

            double[] turns = new double[n];
            bool aligned = Math.Sqrt(offsetX * offsetX + offsetY * offsetY) < alignedThreshold;

            if (!aligned)
            {
                double bestSingleResidual = double.MaxValue;
                int bestSingle = -1;
                double bestSingleTurns = 0;
                for (int i = 0; i < n; i++)
                {
                    double norm = response[i].X * response[i].X + response[i].Y * response[i].Y;
                    if (norm <= 1e-12) continue;

                    double t = -(response[i].X * offsetX + response[i].Y * offsetY) / norm;
                    double rx = offsetX + t * response[i].X;
                    double ry = offsetY + t * response[i].Y;
                    double residual = Math.Sqrt(rx * rx + ry * ry);
                    if (residual < bestSingleResidual)
                    {
                        bestSingleResidual = residual;
                        bestSingle = i;
                        bestSingleTurns = t;
                    }
                }

                if (bestSingle >= 0)
                {
                    turns[bestSingle] = bestSingleTurns;
                }

                if (allowPairs && n >= 3)
                {
                    double bestPairResidual = double.MaxValue;
                    int bestI = -1, bestJ = -1;
                    double bestTi = 0, bestTj = 0;
                    for (int i = 0; i < n - 1; i++)
                    {
                        for (int j = i + 1; j < n; j++)
                        {
                            double a = response[i].X * response[i].X + response[i].Y * response[i].Y;
                            double b = response[i].X * response[j].X + response[i].Y * response[j].Y;
                            double c = response[j].X * response[j].X + response[j].Y * response[j].Y;
                            double det = a * c - b * b;
                            if (a <= 1e-12 || c <= 1e-12 || det < 0.05 * a * c) continue;

                            double ci = response[i].X * offsetX + response[i].Y * offsetY;
                            double cj = response[j].X * offsetX + response[j].Y * offsetY;
                            double ti = (-ci * c + cj * b) / det;
                            double tj = (-cj * a + ci * b) / det;
                            double rx = offsetX + ti * response[i].X + tj * response[j].X;
                            double ry = offsetY + ti * response[i].Y + tj * response[j].Y;
                            double residual = Math.Sqrt(rx * rx + ry * ry);
                            if (residual < bestPairResidual)
                            {
                                bestPairResidual = residual;
                                bestI = i;
                                bestJ = j;
                                bestTi = ti;
                                bestTj = tj;
                            }
                        }
                    }

                    bool pairBetter = bestI >= 0 && (bestSingle < 0 || bestPairResidual < bestSingleResidual * (1.0 - minPairImprovement));
                    if (pairBetter)
                    {
                        Array.Clear(turns);
                        turns[bestI] = bestTi;
                        turns[bestJ] = bestTj;
                    }
                }
            }

            var adjustments = new List<ScrewAdjustment>();
            for (int i = 0; i < n; i++)
            {
                bool active = Math.Abs(turns[i]) > 1e-9;
                adjustments.Add(new ScrewAdjustment
                {
                    Id = i + 1,
                    Direction = active ? (turns[i] > 0 ? "CW" : "CCW") : "Fixed",
                    Magnitude = active ? Math.Abs(turns[i]) : 0,
                    Turns = active ? Math.Abs(turns[i]) : 0
                });
            }

            return adjustments;
        }

        public static List<ScrewAdjustment> CalculateScrewAdjustments(double comaMagnitude, double comaAngleDegrees, double rotationAngleDegrees, int count, double sensitivity = 20.0)
        {
            var adjustments = new List<ScrewAdjustment>();
            if (comaMagnitude < 0.001) // Aligned threshold (0.1% of radius)
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
