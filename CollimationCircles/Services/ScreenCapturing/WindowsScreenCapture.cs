using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace CollimationCircles.Services.ScreenCapturing
{
    internal sealed class WindowsScreenCapture
    {
        private const uint SrcCopy = 0x00CC0020;
        private const uint CaptureBlt = 0x40000000;
        private const uint BiRgb = 0;
        private const uint DibRgbColors = 0;
        private static readonly IntPtr HgdiError = new(-1);

        public ScreenCaptureFrame CaptureArea(int x, int y, int width, int height)
        {
            var stride = checked(width * 4);
            var pixels = new byte[checked(stride * height)];
            var screenDc = GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to get the screen device context.");
            }

            IntPtr memoryDc = IntPtr.Zero;
            IntPtr bitmap = IntPtr.Zero;
            IntPtr previousBitmap = IntPtr.Zero;
            try
            {
                memoryDc = CreateCompatibleDC(screenDc);
                if (memoryDc == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to create a compatible device context.");
                }

                bitmap = CreateCompatibleBitmap(screenDc, width, height);
                if (bitmap == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to create a screen capture bitmap.");
                }

                previousBitmap = SelectObject(memoryDc, bitmap);
                if (previousBitmap == IntPtr.Zero || previousBitmap == HgdiError)
                {
                    previousBitmap = IntPtr.Zero;
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to select the screen capture bitmap.");
                }

                if (!BitBlt(memoryDc, 0, 0, width, height, screenDc, x, y, SrcCopy | CaptureBlt))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to copy pixels from the screen.");
                }

                SelectObject(memoryDc, previousBitmap);
                previousBitmap = IntPtr.Zero;

                var info = new BitmapInfo
                {
                    Header = new BitmapInfoHeader
                    {
                        Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                        Width = width,
                        Height = -height,
                        Planes = 1,
                        BitCount = 32,
                        Compression = BiRgb,
                        SizeImage = (uint)pixels.Length
                    }
                };

                var linesCopied = GetDIBits(memoryDc, bitmap, 0, (uint)height, pixels, ref info, DibRgbColors);
                if (linesCopied != height)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to read pixels from the screen capture bitmap.");
                }

                return new ScreenCaptureFrame(pixels, width, height, stride);
            }
            finally
            {
                if (previousBitmap != IntPtr.Zero)
                {
                    SelectObject(memoryDc, previousBitmap);
                }

                if (bitmap != IntPtr.Zero)
                {
                    DeleteObject(bitmap);
                }

                if (memoryDc != IntPtr.Zero)
                {
                    DeleteDC(memoryDc);
                }

                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hDC);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int width, int height);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool BitBlt(IntPtr hdc, int x, int y, int width, int height, IntPtr sourceDc, int sourceX, int sourceY, uint rasterOperation);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int GetDIBits(IntPtr hdc, IntPtr bitmap, uint startScan, uint scanLines, [Out] byte[] bits, ref BitmapInfo bitmapInfo, uint usage);

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfo
        {
            public BitmapInfoHeader Header;
            public uint Colors;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BitmapInfoHeader
        {
            public uint Size;
            public int Width;
            public int Height;
            public ushort Planes;
            public ushort BitCount;
            public uint Compression;
            public uint SizeImage;
            public int XPelsPerMeter;
            public int YPelsPerMeter;
            public uint ColorsUsed;
            public uint ColorsImportant;
        }
    }

    internal sealed record ScreenCaptureFrame(byte[] Pixels, int Width, int Height, int Stride);
}
