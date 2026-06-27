using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System;
using System.IO;

namespace CollimationCircles.Views
{
    /// <summary>
    /// Custom control that renders JPEG frames from a ZWO camera, scaled to fill
    /// the control bounds.  Bypasses the Avalonia Image control which does not
    /// respect Stretch on macOS.  The JPEG is decoded at the target physical
    /// pixel size so DrawImage can copy 1:1 without scaling.
    /// </summary>
    public class FrameRenderer : Control
    {
        private byte[]? _latestJpeg;
        private Bitmap? _currentBitmap;
        private double _zoom = 1.0;

        public void SetJpegFrame(byte[] jpeg)
        {
            _latestJpeg = jpeg;
            Dispatcher.UIThread.Post(() => InvalidateVisual());
        }

        public double Zoom
        {
            get => _zoom;
            set
            {
                _zoom = value;
                Dispatcher.UIThread.Post(() => InvalidateVisual());
            }
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            if (_latestJpeg is null)
                return;

            double ctrlW = Bounds.Width;
            double ctrlH = Bounds.Height;
            if (ctrlW <= 0 || ctrlH <= 0)
                return;

            double renderScale = 1.0;
            try
            {
                if (VisualRoot is Avalonia.Rendering.IRenderRoot rr)
                    renderScale = rr.RenderScaling;
            }
            catch { }

            int physW = (int)Math.Round(ctrlW * renderScale);
            int physH = (int)Math.Round(ctrlH * renderScale);

            try
            {
                // Probe source dimensions.
                using var probeMs = new MemoryStream(_latestJpeg);
                using var probe = new Bitmap(probeMs);
                int srcW = probe.PixelSize.Width;
                int srcH = probe.PixelSize.Height;
                if (srcW <= 0 || srcH <= 0)
                    return;

                // Decode at the target physical pixel width.
                int targetW;
                if (_zoom <= 1.0)
                {
                    double scale = Math.Min((double)physW / srcW, (double)physH / srcH);
                    targetW = Math.Max(1, (int)Math.Round(srcW * scale));
                }
                else
                {
                    double zoomScale = Math.Min((double)physW / srcW, (double)physH / srcH) * _zoom;
                    targetW = Math.Max(1, (int)Math.Round(srcW * zoomScale));
                }

                using var ms = new MemoryStream(_latestJpeg);
                var decoded = Bitmap.DecodeToWidth(ms, targetW);

                // Swap bitmaps — dispose the previous one.
                var oldBmp = _currentBitmap;
                _currentBitmap = decoded;
                (oldBmp as IDisposable)?.Dispose();

                double dipW = decoded.PixelSize.Width / renderScale;
                double dipH = decoded.PixelSize.Height / renderScale;

                if (_zoom <= 1.0)
                {
                    // Center the bitmap in the control.
                    double offsetX = (ctrlW - dipW) / 2.0;
                    double offsetY = (ctrlH - dipH) / 2.0;

                    using (context.PushTransform(Matrix.CreateTranslation(offsetX, offsetY)))
                    {
                        context.DrawImage(decoded,
                            new Rect(0, 0, decoded.PixelSize.Width, decoded.PixelSize.Height),
                            new Rect(0, 0, dipW, dipH));
                    }
                }
                else
                {
                    // Zoom: crop center and scale to fill the control.
                    int cropW = Math.Min(decoded.PixelSize.Width, physW);
                    int cropH = Math.Min(decoded.PixelSize.Height, physH);
                    int cropX = (decoded.PixelSize.Width - cropW) / 2;
                    int cropY = (decoded.PixelSize.Height - cropH) / 2;

                    double scaleX = ctrlW / (cropW / renderScale);
                    double scaleY = ctrlH / (cropH / renderScale);

                    using (context.PushTransform(Matrix.CreateScale(scaleX, scaleY)))
                    {
                        context.DrawImage(decoded,
                            new Rect(cropX, cropY, cropW, cropH),
                            new Rect(0, 0, cropW / renderScale, cropH / renderScale));
                    }
                }
            }
            catch (Exception ex)
            {
                NLog.LogManager.GetCurrentClassLogger().Error(ex, "FrameRenderer.Render error");
            }
        }
    }
}