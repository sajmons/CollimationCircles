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

        public void SetFrame(CameraFrame frame)
        {
            if (_bitmap is null || _bitmap.PixelSize.Width != frame.Width || _bitmap.PixelSize.Height != frame.Height)
            {
                var old = _bitmap;
                _bitmap = new WriteableBitmap(new PixelSize(frame.Width, frame.Height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
                old?.Dispose();
            }

            using (var fb = _bitmap.Lock())
            {
                var row = new byte[frame.Width * 4];
                for (int y = 0; y < frame.Height; y++)
                {
                    int src = y * frame.Width;
                    for (int x = 0; x < frame.Width; x++)
                    {
                        byte v = frame.Pixels[src + x];
                        int o = x * 4;
                        row[o] = v;
                        row[o + 1] = v;
                        row[o + 2] = v;
                        row[o + 3] = 255;
                    }
                    Marshal.Copy(row, 0, fb.Address + y * fb.RowBytes, row.Length);
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
    }
}
