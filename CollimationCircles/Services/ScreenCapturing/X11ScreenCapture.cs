using System;
using System.Runtime.InteropServices;

namespace CollimationCircles.Services.ScreenCapturing
{
    /// <summary>
    /// Captures screen pixels from the X11 root window with XGetImage.
    /// </summary>
    internal sealed class X11ScreenCapture : IScreenCaptureBackend
    {
        private const string LibX11 = "libX11.so.6";
        private const int ZPixmap = 2;
        private const nuint RedMask = 0xFF0000;
        private const nuint GreenMask = 0x00FF00;
        private const nuint BlueMask = 0x0000FF;

        public string Name => "X11 XGetImage";

        public ScreenCaptureFrame Capture(CaptureRegion region)
        {
            var display = XOpenDisplay(null);
            if (display == IntPtr.Zero)
            {
                throw new InvalidOperationException("Unable to open the X11 display.");
            }

            try
            {
                var screen = XDefaultScreen(display);
                var root = XRootWindow(display, screen);

                // XGetImage raises a fatal X error for areas outside the root window, so the area is clamped.
                var bounds = region.Bounds;
                var left = Math.Max(0, bounds.X);
                var top = Math.Max(0, bounds.Y);
                var right = Math.Min(XDisplayWidth(display, screen), bounds.Right);
                var bottom = Math.Min(XDisplayHeight(display, screen), bounds.Bottom);
                var width = right - left;
                var height = bottom - top;
                if (width <= 0 || height <= 0)
                {
                    throw new InvalidOperationException("The capture area is outside of the screen.");
                }

                var imagePtr = XGetImage(display, root, left, top, (uint)width, (uint)height, nuint.MaxValue, ZPixmap);
                if (imagePtr == IntPtr.Zero)
                {
                    throw new InvalidOperationException("XGetImage failed.");
                }

                try
                {
                    var image = Marshal.PtrToStructure<XImage>(imagePtr);
                    if (image.bits_per_pixel != 32 || image.red_mask != RedMask || image.green_mask != GreenMask || image.blue_mask != BlueMask)
                    {
                        throw new NotSupportedException($"Unsupported X11 pixel layout: {image.bits_per_pixel} bpp, masks {image.red_mask:X}/{image.green_mask:X}/{image.blue_mask:X}.");
                    }

                    var stride = width * 4;
                    if (image.bytes_per_line < stride)
                    {
                        throw new InvalidOperationException("Unexpected X11 image row size.");
                    }

                    var pixels = new byte[checked(stride * height)];
                    for (var row = 0; row < height; row++)
                    {
                        Marshal.Copy(IntPtr.Add(image.data, row * image.bytes_per_line), pixels, row * stride, stride);
                    }

                    // The unused byte of 24-bit depth visuals is undefined, so the frame is made opaque.
                    for (var i = 3; i < pixels.Length; i += 4)
                    {
                        pixels[i] = 0xFF;
                    }

                    return new ScreenCaptureFrame(pixels, width, height, stride);
                }
                finally
                {
                    XDestroyImage(imagePtr);
                }
            }
            finally
            {
                XCloseDisplay(display);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XImage
        {
            public int width;
            public int height;
            public int xoffset;
            public int format;
            public IntPtr data;
            public int byte_order;
            public int bitmap_unit;
            public int bitmap_bit_order;
            public int bitmap_pad;
            public int depth;
            public int bytes_per_line;
            public int bits_per_pixel;
            public nuint red_mask;
            public nuint green_mask;
            public nuint blue_mask;
        }

        [DllImport(LibX11)]
        private static extern IntPtr XOpenDisplay(string? displayName);

        [DllImport(LibX11)]
        private static extern int XCloseDisplay(IntPtr display);

        [DllImport(LibX11)]
        private static extern int XDefaultScreen(IntPtr display);

        [DllImport(LibX11)]
        private static extern nuint XRootWindow(IntPtr display, int screen);

        [DllImport(LibX11)]
        private static extern int XDisplayWidth(IntPtr display, int screen);

        [DllImport(LibX11)]
        private static extern int XDisplayHeight(IntPtr display, int screen);

        [DllImport(LibX11)]
        private static extern IntPtr XGetImage(IntPtr display, nuint drawable, int x, int y, uint width, uint height, nuint planeMask, int format);

        [DllImport(LibX11)]
        private static extern int XDestroyImage(IntPtr image);
    }
}
