using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CollimationCircles.Messages;
using System;
using System.Runtime.InteropServices;

namespace CollimationCircles.Views
{
    /// <summary>
    /// Renders 8-bit grayscale camera frames into a reusable WriteableBitmap,
    /// scaled uniformly to fit the control bounds.
    /// </summary>
    public class FrameRenderer : Control
    {
        private WriteableBitmap? _bitmap;
        private byte[]? _bgraBuffer;

        public void SetFrame(CameraFrame frame)
        {
            if (_bitmap is null || _bitmap.PixelSize.Width != frame.Width || _bitmap.PixelSize.Height != frame.Height)
            {
                var old = _bitmap;
                _bitmap = new WriteableBitmap(new PixelSize(frame.Width, frame.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
                old?.Dispose();
                _bgraBuffer = new byte[frame.Width * frame.Height * 4];
            }

            var bgra = _bgraBuffer ??= new byte[frame.Width * frame.Height * 4];
            ExpandGrayToBgra(frame.Pixels, bgra);

            using (var fb = _bitmap.Lock())
            {
                int copyBytesPerRow = frame.Width * 4;
                if (fb.RowBytes == copyBytesPerRow)
                {
                    Marshal.Copy(bgra, 0, fb.Address, bgra.Length);
                }
                else
                {
                    for (int y = 0; y < frame.Height; y++)
                    {
                        Marshal.Copy(bgra, y * copyBytesPerRow, fb.Address + y * fb.RowBytes, copyBytesPerRow);
                    }
                }
            }

            InvalidateVisual();
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (_bitmap is null)
                return;

            double ctrlW = Bounds.Width;
            double ctrlH = Bounds.Height;
            if (ctrlW <= 0 || ctrlH <= 0)
                return;

            var size = _bitmap.PixelSize;
            double scale = Math.Min(ctrlW / size.Width, ctrlH / size.Height);
            double w = size.Width * scale;
            double h = size.Height * scale;
            context.DrawImage(_bitmap,
                new Rect(0, 0, size.Width, size.Height),
                new Rect((ctrlW - w) / 2, (ctrlH - h) / 2, w, h));
        }

        private static void ExpandGrayToBgra(byte[] gray, byte[] bgra)
        {
            int src = 0;
            int dst = 0;
            while (src < gray.Length)
            {
                byte value = gray[src++];
                bgra[dst++] = value;
                bgra[dst++] = value;
                bgra[dst++] = value;
                bgra[dst++] = 255;
            }
        }
    }
}
