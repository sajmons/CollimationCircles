using System;
using System.Runtime.InteropServices;

namespace CollimationCircles.Services.ScreenCapturing
{
    /// <summary>
    /// Captures screen pixels with CoreGraphics. Requires the Screen Recording permission.
    /// </summary>
    internal sealed class MacOsScreenCapture : IScreenCaptureBackend
    {
        private const string CoreGraphics = "/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics";

        private const uint WindowListOnScreenOnly = 1;
        private const uint NullWindowId = 0;
        private const uint ImageDefault = 0;

        // kCGImageAlphaPremultipliedFirst | kCGBitmapByteOrder32Little, i.e. BGRA in memory.
        private const uint BitmapInfoBgra = 2 | (2 << 12);

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private bool accessChecked;

        public string Name => "macOS CoreGraphics";

        public ScreenCaptureFrame Capture(CaptureRegion region)
        {
            EnsureScreenRecordingAccess();

            // CoreGraphics addresses the screen in points, Avalonia in physical pixels.
            var scale = region.Scaling > 0 ? region.Scaling : 1.0;
            var bounds = region.Bounds;
            var rect = new CGRect(bounds.X / scale, bounds.Y / scale, bounds.Width / scale, bounds.Height / scale);

            var image = CGWindowListCreateImage(rect, WindowListOnScreenOnly, NullWindowId, ImageDefault);
            if (image == IntPtr.Zero)
            {
                throw new InvalidOperationException("CoreGraphics returned no image. Check the Screen Recording permission.");
            }

            try
            {
                var width = checked((int)CGImageGetWidth(image));
                var height = checked((int)CGImageGetHeight(image));
                if (width <= 0 || height <= 0)
                {
                    throw new InvalidOperationException("CoreGraphics returned an empty image.");
                }

                var stride = checked(width * 4);
                var pixels = new byte[checked(stride * height)];

                // Drawing into a context with a known layout avoids depending on the source image's pixel format.
                var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
                IntPtr colorSpace = IntPtr.Zero;
                IntPtr context = IntPtr.Zero;
                try
                {
                    colorSpace = CGColorSpaceCreateDeviceRGB();
                    context = CGBitmapContextCreate(handle.AddrOfPinnedObject(), (nuint)width, (nuint)height, 8, (nuint)stride, colorSpace, BitmapInfoBgra);
                    if (context == IntPtr.Zero)
                    {
                        throw new InvalidOperationException("Unable to create a CoreGraphics bitmap context.");
                    }

                    CGContextDrawImage(context, new CGRect(0, 0, width, height), image);
                }
                finally
                {
                    if (context != IntPtr.Zero)
                    {
                        CGContextRelease(context);
                    }

                    if (colorSpace != IntPtr.Zero)
                    {
                        CGColorSpaceRelease(colorSpace);
                    }

                    handle.Free();
                }

                return new ScreenCaptureFrame(pixels, width, height, stride);
            }
            finally
            {
                CGImageRelease(image);
            }
        }

        private void EnsureScreenRecordingAccess()
        {
            if (accessChecked)
            {
                return;
            }

            accessChecked = true;
            if (!CGPreflightScreenCaptureAccess())
            {
                logger.Warn("Screen Recording permission is not granted; requesting it.");
                CGRequestScreenCaptureAccess();
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct CGRect(double x, double y, double width, double height)
        {
            public readonly double X = x;
            public readonly double Y = y;
            public readonly double Width = width;
            public readonly double Height = height;
        }

        [DllImport(CoreGraphics)]
        private static extern IntPtr CGWindowListCreateImage(CGRect screenBounds, uint listOption, uint windowId, uint imageOption);

        [DllImport(CoreGraphics)]
        private static extern nuint CGImageGetWidth(IntPtr image);

        [DllImport(CoreGraphics)]
        private static extern nuint CGImageGetHeight(IntPtr image);

        [DllImport(CoreGraphics)]
        private static extern void CGImageRelease(IntPtr image);

        [DllImport(CoreGraphics)]
        private static extern IntPtr CGColorSpaceCreateDeviceRGB();

        [DllImport(CoreGraphics)]
        private static extern void CGColorSpaceRelease(IntPtr colorSpace);

        [DllImport(CoreGraphics)]
        private static extern IntPtr CGBitmapContextCreate(IntPtr data, nuint width, nuint height, nuint bitsPerComponent, nuint bytesPerRow, IntPtr colorSpace, uint bitmapInfo);

        [DllImport(CoreGraphics)]
        private static extern void CGContextDrawImage(IntPtr context, CGRect rect, IntPtr image);

        [DllImport(CoreGraphics)]
        private static extern void CGContextRelease(IntPtr context);

        [DllImport(CoreGraphics)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool CGPreflightScreenCaptureAccess();

        [DllImport(CoreGraphics)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool CGRequestScreenCaptureAccess();
    }
}
