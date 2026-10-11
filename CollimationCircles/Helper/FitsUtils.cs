using CollimationCircles.ViewModels;
using CommunityToolkit.Mvvm.DependencyInjection;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace CollimationCircles.Helper
{
    public enum BayerPatternMode
    {
        Auto,
        None,
        RGGB,
        BGGR,
        GRBG,
        GBRG
    }

    internal class FitsUtils
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public static BayerPatternMode ParseBayerPattern(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return BayerPatternMode.Auto;
            }

            var normalized = value.Trim();
            if (normalized.Equals("Auto", StringComparison.OrdinalIgnoreCase)) return BayerPatternMode.Auto;
            if (normalized.Equals("None", StringComparison.OrdinalIgnoreCase) || normalized.Equals("Mono", StringComparison.OrdinalIgnoreCase)) 
                return BayerPatternMode.None;
            if (normalized.Equals("RGGB", StringComparison.OrdinalIgnoreCase)) return BayerPatternMode.RGGB;
            if (normalized.Equals("BGGR", StringComparison.OrdinalIgnoreCase)) return BayerPatternMode.BGGR;
            if (normalized.Equals("GRBG", StringComparison.OrdinalIgnoreCase)) return BayerPatternMode.GRBG;
            if (normalized.Equals("GBRG", StringComparison.OrdinalIgnoreCase)) return BayerPatternMode.GBRG;

            return BayerPatternMode.Auto;
        }

        public static Mat? ToFrame(Array? data, BayerPatternMode bayerPattern)
        {
            if (data is null)
            {
                return null;
            }

            return data switch
            {
                byte[,] p => ToFrame2D(p, bayerPattern),
                short[,] p => ToFrame2D(p, bayerPattern),
                ushort[,] p => ToFrame2D(p, bayerPattern),
                int[,] p => ToFrame2D(p, bayerPattern),
                uint[,] p => ToFrame2D(p, bayerPattern),
                float[,] p => ToFrame2D(p, bayerPattern),
                double[,] p => ToFrame2D(p, bayerPattern),
                byte[,,] p => ToFrame3D(p),
                short[,,] p => ToFrame3D(p),
                ushort[,,] p => ToFrame3D(p),
                int[,,] p => ToFrame3D(p),
                uint[,,] p => ToFrame3D(p),
                float[,,] p => ToFrame3D(p),
                double[,,] p => ToFrame3D(p),
                _ => ToFrameFallback(data, bayerPattern)
            };
        }

        private static Mat? ToFrame2D<T>(T[,] data, BayerPatternMode bayerPattern)
            where T : IConvertible
        {
            int width = data.GetLength(0);
            int height = data.GetLength(1);
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            int total = width * height;
            var values = new double[total];
            double min = double.MaxValue;
            double max = double.MinValue;
            int idx = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double value = data[x, y].ToDouble(null);
                    values[idx++] = value;
                    if (value < min) min = value;
                    if (value > max) max = value;
                }
            }

            var normalized = CreateNormalizedFrame(width, height, values, min, max);
            return ApplyOpenCvDemosaicIfNeeded(normalized, bayerPattern);
        }

        private static Mat? ToFrame3D<T>(T[,,] data)
            where T : IConvertible
        {
            int width = data.GetLength(0);
            int height = data.GetLength(1);
            int planes = data.GetLength(2);

            if (width <= 0 || height <= 0 || planes <= 0)
            {
                return null;
            }

            int total = width * height;
            var values = new double[total];
            double min = double.MaxValue;
            double max = double.MinValue;
            int idx = 0;
            double invPlanes = 1.0 / planes;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double sum = 0;
                    for (int p = 0; p < planes; p++)
                    {
                        sum += data[x, y, p].ToDouble(null);
                    }

                    double value = sum * invPlanes;
                    values[idx++] = value;
                    if (value < min) min = value;
                    if (value > max) max = value;
                }
            }

            return CreateNormalizedFrame(width, height, values, min, max);
        }

        private static Mat? ToFrameFallback(Array data, BayerPatternMode bayerPattern)
        {
            if (data.Rank is < 2 or > 3)
            {
                return null;
            }

            int width = data.GetLength(0);
            int height = data.GetLength(1);
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            int total = width * height;
            var values = new double[total];
            double min = double.MaxValue;
            double max = double.MinValue;
            int idx = 0;

            if (data.Rank == 2)
            {
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        double value = Convert.ToDouble(data.GetValue(x, y));
                        values[idx++] = value;
                        if (value < min) min = value;
                        if (value > max) max = value;
                    }
                }

                var normalized2D = CreateNormalizedFrame(width, height, values, min, max);
                return ApplyOpenCvDemosaicIfNeeded(normalized2D, bayerPattern);
            }
            else
            {
                int planes = data.GetLength(2);
                if (planes <= 0)
                {
                    return null;
                }

                double invPlanes = 1.0 / planes;
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        double sum = 0;
                        for (int p = 0; p < planes; p++)
                        {
                            sum += Convert.ToDouble(data.GetValue(x, y, p));
                        }

                        double value = sum * invPlanes;
                        values[idx++] = value;
                        if (value < min) min = value;
                        if (value > max) max = value;
                    }
                }
            }

            return CreateNormalizedFrame(width, height, values, min, max);
        }

        private static Mat ApplyOpenCvDemosaicIfNeeded(Mat frame, BayerPatternMode bayerPattern)
        {
            var conversion = GetBayerToBgrConversionCode(bayerPattern);
            if (conversion is null)
            {
                return frame;
            }

            try
            {
                using var source = new Mat(frame.Height, frame.Width, MatType.CV_8UC1);

                var pixels = new byte[source.Total() * source.ElemSize()];
                source.GetArray(out pixels);
                CopySingleChannelPixelsToMat(pixels, source, frame.Width, frame.Height);
                using var bgr = new Mat();
                Cv2.CvtColor(source, bgr, conversion.Value);

                using var gray = new Mat();
                Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);

                var pixelsOut = CopySingleChannelMat(gray, frame.Width, frame.Height);

                var mat = new Mat(frame.Height, frame.Width, MatType.CV_8UC1);
                mat.SetArray(pixelsOut);
                return mat;
            }
            catch (Exception ex)
            {
                logger.Warn($"OpenCV Bayer conversion failed for '{bayerPattern}', using raw grayscale frame: {ex.Message}");
                return frame;
            }
        }

        private static ColorConversionCodes? GetBayerToBgrConversionCode(BayerPatternMode bayerPattern)
        {
            return bayerPattern switch
            {
                BayerPatternMode.RGGB => ColorConversionCodes.BayerRG2BGR,
                BayerPatternMode.BGGR => ColorConversionCodes.BayerBG2BGR,
                BayerPatternMode.GRBG => ColorConversionCodes.BayerGR2BGR,
                BayerPatternMode.GBRG => ColorConversionCodes.BayerGB2BGR,
                _ => null
            };
        }

        private static byte[] CopySingleChannelMat(Mat mat, int width, int height)
        {
            var pixels = new byte[width * height];
            if (mat.Step() == width)
            {
                Marshal.Copy(mat.Data, pixels, 0, pixels.Length);
                return pixels;
            }

            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(mat.Data + (int)(y * mat.Step()), pixels, y * width, width);
            }

            return pixels;
        }

        private static void CopySingleChannelPixelsToMat(byte[] pixels, Mat mat, int width, int height)
        {
            if (mat.Step() == width)
            {
                Marshal.Copy(pixels, 0, mat.Data, pixels.Length);
                return;
            }

            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(pixels, y * width, mat.Data + (int)(y * mat.Step()), width);
            }
        }

        private static Mat CreateNormalizedFrame(int width, int height, double[] values, double min, double max)
        {
            Mat mat;

            var pixels = new byte[values.Length];
            double range = max - min;
            if (range <= 0)
            {
                mat = new Mat(height, width, MatType.CV_8UC1);
                mat.SetArray(pixels);
                return mat;
            }

            double scale = 255.0 / range;
            for (int i = 0; i < values.Length; i++)
            {
                pixels[i] = (byte)((values[i] - min) * scale);
            }

            mat = new Mat(height, width, MatType.CV_8UC1);
            mat.SetArray(pixels);

            return mat;
        }
    }
}
