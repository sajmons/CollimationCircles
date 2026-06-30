using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Threading;
using DirectX.Capture;
using DShowNET;

namespace DxWebCam
{
    /// <summary>
    /// The WebCam class gives easy access to the WebCam and video files for use as an input to deep learning platforms such as the MyCaffe AI Platform.
    /// @see [MyCaffe: A Complete C# Re-Write of Caffe with Reinforcement Learning](https://arxiv.org/abs/1810.02272) by David W. Brown, arXiv:1810.02272, 2018
    /// </summary>
    public class WebCam : ISampleGrabberCB, IDisposable
    {
        IBaseFilter m_camFilter;
        IBaseFilter m_videoFilter;
        IFilterGraph2 m_graphBuilder;
        IAMCameraControl m_camControl;
        ICaptureGraphBuilder2 m_captureGraphBuilder;      
        ISampleGrabber m_sampleGrabber;
        IMediaControl m_mediaControl;
        IMediaEventEx m_mediaEventEx;
        IVideoWindow m_videoWindow;
        IVideoFrameStep m_videoFrameStep;
        IBaseFilter m_baseGrabFilter;
        IMediaSeeking m_mediaSeek;
        IBaseFilter m_nullRenderer = null;
        VideoInfoHeader m_videoInfoHeader;
        Filters m_filters = new Filters();
        Filter m_selectedFilter;
        byte[] m_rgBuffer = null;
        AutoResetEvent m_evtImageSnapped = new AutoResetEvent(false);
        bool m_bSnapEnabled = false;
        bool m_bInvertImage = false;
        bool m_bRunning = false;
        bool m_bConnected = false;
        long m_lDuration = 0;
        bool m_bAutoResize = true;

        const int WS_CHILD = 0x40000000;
        const int WS_CLIPCHILDREN = 0x02000000;
        const int WS_CLIPSIBLINGS = 0x04000000;
        const int WM_GRAPHNOTIFY = 0x8000 + 1;

        /// <summary>
        /// The OnSnapshot event fires when calling the GetImage method.
        /// </summary>
        public event EventHandler<ImageArgs> OnSnapshot;

        /// <summary>
        /// The constructor.
        /// </summary>
        public WebCam()
        {
        }

        /// <summary>
        /// Cleanup all resources used by closing the video feed.
        /// </summary>
        public void Dispose()
        {
            Close();
        }

        /// <summary>
        /// Get/set whether or not to subscribe to the target picturebox size changed event and automatically resize the video window.
        /// </summary>
        public bool AutoResize
        {
            get { return m_bAutoResize; }
            set { m_bAutoResize = value; }
        }

        /// <summary>
        /// Get/set whether or not to invert the images received.
        /// </summary>
        public bool InvertImage
        {
            get { return m_bInvertImage; }
            set { m_bInvertImage = value; }
        }

        /// <summary>
        /// Return the video compressors found.
        /// </summary>
        public FilterCollection VideoCompressors
        {
            get { return m_filters.VideoCompressors; }
        }

        /// <summary>
        /// Return the video filters found.
        /// </summary>
        public FilterCollection VideoInputDevices
        {
            get { return m_filters.VideoInputDevices; }
        }

        /// <summary>
        /// Return whether or not the video feed is open or not.
        /// </summary>
        public bool IsConnected
        {
            get { return m_bConnected;  }
        }

        /// <summary>
        /// Returns the video capabilities of the video device.
        /// </summary>
        /// <param name="filter">Specifies the video device.</param>
        /// <returns>A collection of video capabilities is returned for the device.</returns>
        public VideoCapabilityCollection GetVideoCapatiblities(Filter filter)
        {
            int hr;
            IFilterGraph2 grph = null;
            IBaseFilter camFltr = null;
            ICaptureGraphBuilder2 bldr = null;
            object comObj = null;
            AMMediaType mt = null;
            IntPtr pSC = IntPtr.Zero;
            VideoCapabilityCollection colCap = new VideoCapabilityCollection();

            try
            {
                if (filter == null)
                    return colCap;

                grph = (IFilterGraph2)Activator.CreateInstance(Type.GetTypeFromCLSID(Clsid.FilterGraph, true));

                IMoniker moniker = filter.CreateMoniker();
                grph.AddSourceFilterForMoniker(moniker, null, filter.Name, out camFltr);
                Marshal.ReleaseComObject(moniker);

                bldr = (ICaptureGraphBuilder2)Activator.CreateInstance(Type.GetTypeFromCLSID(Clsid.CaptureGraphBuilder2, true));
                hr = bldr.SetFiltergraph(grph as IGraphBuilder);
                if (hr < 0)
                    Marshal.ThrowExceptionForHR(hr);

                // Add the web-cam filter to the graph.
                hr = grph.AddFilter(camFltr, filter.Name);
                if (hr < 0)
                    Marshal.ThrowExceptionForHR(hr);

                // Get the IAMStreamConfig interface.
                Guid cat = PinCategory.Capture;
                Guid type = MediaType.Interleaved;
                Guid iid = typeof(IAMStreamConfig).GUID;

                hr = bldr.FindInterface(ref cat, ref type, camFltr, ref iid, out comObj);
                if (hr < 0)
                {
                    type = MediaType.Video;
                    hr = bldr.FindInterface(ref cat, ref type, camFltr, ref iid, out comObj);
                }

                if (hr < 0)
                    Marshal.ThrowExceptionForHR(hr);

                IAMStreamConfig cfg = comObj as IAMStreamConfig;


                // Enumerate the video capabilities.
                int nCount;
                int nSize;
                hr = cfg.GetNumberOfCapabilities(out nCount, out nSize);
                if (hr < 0)
                    Marshal.ThrowExceptionForHR(hr);

                VideoInfoHeader vih = new VideoInfoHeader();
                VideoStreamConfigCaps vsc = new VideoStreamConfigCaps();
                pSC = Marshal.AllocCoTaskMem(nSize);

                for (int i = 0; i < nCount; i++)
                {
                    IntPtr pMT;
                    hr = cfg.GetStreamCaps(i, out pMT, pSC);
                    if (hr < 0)
                        Marshal.ThrowExceptionForHR(hr);

                    mt = Marshal.PtrToStructure<AMMediaType>(pMT);

                    Marshal.PtrToStructure(mt.formatPtr, vih);
                    Marshal.PtrToStructure(pSC, vsc);

                    int nWidth = vih.BmiHeader.Width;
                    int nHeight = vih.BmiHeader.Height;
                    int nFpsMin = (int)(10000000 / vsc.MaxFrameInterval);
                    int nFpsMax = (int)(10000000 / vsc.MinFrameInterval);

                    colCap.Add(new VideoCapability(nWidth, nHeight, nFpsMin, nFpsMax));

                    if (mt != null)
                    {
                        Marshal.FreeCoTaskMem(mt.formatPtr);
                        mt = null;
                    }
                }
            }
            catch (Exception excpt)
            {
                throw excpt;
            }
            finally
            {
                if (mt != null)
                    Marshal.FreeCoTaskMem(mt.formatPtr);

                if (comObj != null)
                    Marshal.ReleaseComObject(comObj);

                if (pSC != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pSC);

                if (bldr != null)
                    Marshal.ReleaseComObject(bldr);

                if (camFltr != null)
                    Marshal.ReleaseComObject(camFltr);

                if (grph != null)
                    Marshal.ReleaseComObject(grph);
            }

            return colCap;
        }        

        /// <summary>
        /// Set the video capabilities.
        /// </summary>
        /// <param name="bldr">Specifies the capture builder</param>
        /// <param name="flt">Specifies the video filter.</param>
        /// <param name="vidCap">Specifies the desired capabilities.</param>
        /// <returns><i>true</i> is returned if set, otherwise <i>false</i>.</returns>
        /// <remarks>
        /// @see http://blog.dvdbuilder.com/setting-video-capture-format-directshow-net
        /// </remarks>
        private bool setVideoCapabilities(ICaptureGraphBuilder2 bldr, IBaseFilter flt, VideoCapability vidCap)
        {
            int hr;
            Guid cat = PinCategory.Capture;
            Guid type = MediaType.Interleaved;
            Guid iid = typeof(IAMStreamConfig).GUID;
            object comObj = null;
            IntPtr pSC = IntPtr.Zero;
            AMMediaType mt = null;

            try
            {
                hr = bldr.FindInterface(ref cat, ref type, flt, ref iid, out comObj);
                if (hr != 0)
                {
                    type = MediaType.Video;
                    hr = bldr.FindInterface(ref cat, ref type, flt, ref iid, out comObj);
                }

                if (hr < 0)
                    Marshal.ThrowExceptionForHR(hr);

                IAMStreamConfig cfg = comObj as IAMStreamConfig;
                int nCount;
                int nSize;
                hr = cfg.GetNumberOfCapabilities(out nCount, out nSize);
                if (hr < 0)
                    Marshal.ThrowExceptionForHR(hr);

                VideoInfoHeader vih = new VideoInfoHeader();
                VideoStreamConfigCaps vsc = new VideoStreamConfigCaps();
                pSC = Marshal.AllocCoTaskMem(nSize);

                for (int i = 0; i < nCount; i++)
                {
                    mt = null;

                    IntPtr pMT;
                    hr = cfg.GetStreamCaps(i, out pMT, pSC);
                    if (hr == 0)
                    {
                        mt = Marshal.PtrToStructure<AMMediaType>(pMT);

                        Marshal.PtrToStructure(mt.formatPtr, vih);
                        Marshal.PtrToStructure(pSC, vsc);

                        int nMinFps = (int)(10000000 / vsc.MaxFrameInterval);
                        int nMaxFps = (int)(10000000 / vsc.MinFrameInterval);

                        if ((vih.BmiHeader.Width == vidCap.Width || vidCap.Width == 0) &&
                            (vih.BmiHeader.Height == vidCap.Height || vidCap.Height == 0) &&
                            ((nMinFps <= vidCap.TargetFPS && nMaxFps >= vidCap.TargetFPS) || vidCap.TargetFPS == 0))
                            break;
                    }

                    if (mt != null)
                    {
                        Marshal.FreeCoTaskMem(mt.formatPtr);
                        mt = null;
                    }
                }

                if (mt == null)
                    return false;

                cfg.SetFormat(mt);
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                if (comObj != null)
                    Marshal.ReleaseComObject(comObj);

                if (pSC != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(pSC);

                if (mt != null)
                    Marshal.FreeCoTaskMem(mt.formatPtr);
            }

            return true;
        }        

        /// <summary>
        /// Close the currently open video feed.
        /// </summary>
        public void Close()
        {
            m_bConnected = false;
            m_lDuration = 0;

            if (m_mediaControl != null)
                m_mediaControl.Stop();

            if (m_mediaEventEx != null)
                m_mediaEventEx.SetNotifyWindow(IntPtr.Zero, WM_GRAPHNOTIFY, IntPtr.Zero);

            if (m_videoWindow != null)
            {
                m_videoWindow.put_Visible(DsHlp.OAFALSE);
                m_videoWindow.put_Owner(IntPtr.Zero);
            }

            // Release all interfaces.

            if (m_graphBuilder != null)
            {
                Marshal.ReleaseComObject(m_graphBuilder);
                m_graphBuilder = null;
            }

            if (m_camControl != null)
            {
                Marshal.ReleaseComObject(m_camControl);
                m_camControl = null;
            }

            if (m_captureGraphBuilder != null)
            {
                Marshal.ReleaseComObject(m_captureGraphBuilder);
                m_captureGraphBuilder = null;
            }

            if (m_mediaSeek != null)
            {
                Marshal.ReleaseComObject(m_mediaSeek);
                m_mediaSeek = null;
            }

            if (m_videoFrameStep != null)
            {
                Marshal.ReleaseComObject(m_videoFrameStep);
                m_videoFrameStep = null;
            }

            if (m_sampleGrabber != null)
            {
                Marshal.ReleaseComObject(m_sampleGrabber);
                m_sampleGrabber = null;
            }

            if (m_baseGrabFilter != null)
            {
                Marshal.ReleaseComObject(m_baseGrabFilter);
                m_baseGrabFilter = null;
            }

            if (m_mediaControl != null)
            {
                Marshal.ReleaseComObject(m_mediaControl);
                m_mediaControl = null;
            }

            if (m_mediaEventEx != null)
            {
                Marshal.ReleaseComObject(m_mediaEventEx);
                m_mediaEventEx = null;
            }

            if (m_videoWindow != null)
            {
                Marshal.ReleaseComObject(m_videoWindow);
                m_videoWindow = null;
            }

            if (m_nullRenderer != null)
            {
                Marshal.ReleaseComObject(m_nullRenderer);
                m_nullRenderer = null;
            }

            if (m_videoFilter != null)
            {
                Marshal.ReleaseComObject(m_videoFilter);
                m_videoFilter = null;
            }
        }

        /// <summary>
        /// Step a specified number of frames in the feed (this function only applies to a videw file feed).
        /// </summary>
        /// <param name="nFrames">Specifies the number of frames to step.</param>
        /// <returns>After a successful step <i>true</i> is returned, otherwise when ignored or not running <i>false</i> is returned.</returns>
        public bool Step(int nFrames)
        {
            if (m_mediaSeek == null)
                return false;

            if (m_bRunning)
                return false;

            if (m_videoFrameStep != null)
            {
                int hr = m_videoFrameStep.Step(nFrames, null);
                if (hr < 0)
                    Marshal.ThrowExceptionForHR(hr);
            }
            else
            {
                long lPosition;
                int hr = m_mediaSeek.GetCurrentPosition(out lPosition);

                long lStep = m_videoInfoHeader.AvgTimePerFrame * nFrames;
                long lNewPosition = lPosition + lStep;

                if (lNewPosition > Duration)
                    lNewPosition = Duration;

                SetPosition(lNewPosition);
            }

            return true;
        }

        /// <summary>
        /// Play a video file (does not apply to a web-cam feed).
        /// </summary>
        /// <returns>After a successful initiated play <i>true</i> is returned, otherwise when ignored or not running <i>false</i> is returned.</returns>
        public bool Play()
        {
            if (m_mediaControl == null)
                return false;

            int hr = m_mediaControl.Run();
            if (hr < 0)
                Marshal.ThrowExceptionForHR(hr);

            m_bRunning = true;

            return true;
        }

        /// <summary>
        /// Stop a video file from playing. (does not apply to a web-cam feed).
        /// </summary>
        /// <returns>After a successful stop <i>true</i> is returned, otherwise when ignored or not running <i>false</i> is returned.</returns>
        public bool Stop()
        {
            if (m_mediaControl == null)
                return false;

            int hr = m_mediaControl.Stop();
            if (hr < 0)
                Marshal.ThrowExceptionForHR(hr);

            m_bRunning = false;

            return true;
        }

        /// <summary>
        /// Returns whether or not a video file is at its end.  When using a web-cam, this function always returns <i>false</i>.
        /// </summary>
        public bool IsAtEnd
        {
            get
            {
                if (m_mediaSeek == null)
                    return false;

                long lCurrent = CurrentPosition;
                if (lCurrent == m_lDuration)
                    return true;

                return false;
            }
        }

        /// <summary>
        /// Returns the percentage of video that has already been played.  When using a web-cam, this function always returns 0.
        /// </summary>
        public double CompletionPercent
        {
            get
            {
                if (m_mediaSeek == null)
                    return 0;

                long lCurrent = CurrentPosition;
                double dfPct = (Duration == 0) ? 0 : (double)lCurrent / (double)Duration;
                return dfPct;
            }
        }

        /// <summary>
        /// Returns the duration of a video file.  When using a web-cam, this function always returns 0.
        /// </summary>
        public long Duration
        {
            get { return m_lDuration; }
        }

        /// <summary>
        /// Returns the current position of a video file.  When using a web-cam, this function always returns 0.
        /// </summary>
        public long CurrentPosition
        {
            get
            {
                if (m_mediaSeek == null)
                    return 0;

                long lPosition;
                int hr = m_mediaSeek.GetCurrentPosition(out lPosition);
                if (hr < 0)
                    Marshal.ThrowExceptionForHR(hr);

                return lPosition;
            }
        }

        /// <summary>
        /// Set the current position within a video file to the specified position.  This function is ignored when using a web-cam.
        /// </summary>
        /// <param name="lPosition">Specifies the new position to set.</param>
        public void SetPosition(long lPosition)
        {
            if (m_mediaSeek == null)
                return;

            long lDuration;
            int hr = m_mediaSeek.GetDuration(out lDuration);
            if (hr < 0)
                Marshal.ThrowExceptionForHR(hr);

            if (lPosition < 0 || lPosition > lDuration)
                throw new Exception("The postion specified is outside of the video duration range [0," + lDuration.ToString() + "].  Please specify a valid position.");

            DsOptInt64 pos = new DsOptInt64(lPosition);
            DsOptInt64 stop = new DsOptInt64(lDuration);
            hr = m_mediaSeek.SetPositions(pos, SeekingFlags.AbsolutePositioning, stop, SeekingFlags.AbsolutePositioning);
            if (hr < 0)
                Marshal.ThrowExceptionForHR(hr);
        }

        /// <summary>
        /// Set the web-cam to a given focus.  This function is ignored when using a video file.
        /// </summary>
        /// <param name="nVal">Specifies the focus value.</param>
        public void SetFocus(int nVal)
        {
            if (m_camControl != null)
                m_camControl.Set(CameraControlProperty.Focus, nVal, CameraControlFlags.Manual);
        }

        /// <summary>
        /// Get the focus value of the web-cam.  This function always returns -1 when using a video file.
        /// </summary>
        /// <returns>The focus value is returned.</returns>
        public int GetFocus()
        {
            int nVal = -1;
            CameraControlFlags flags;

            if (m_camControl != null)
                return m_camControl.Get(CameraControlProperty.Focus, out nVal, out flags);

            return nVal;
        }

        /// <summary>
        /// Get a snapshot of the video or webcam.
        /// </summary>
        /// <param name="nMsMaxWait">Optionally, specifies the maximum amount of milliseconds to wait to get the next image (default = 10000).</param>
        public void GetImage(int nMsMaxWait = 10000)
        {
            int nSize = m_videoInfoHeader.BmiHeader.ImageSize;
            if (m_rgBuffer == null || m_rgBuffer.Length != nSize + 63999)
                m_rgBuffer = new byte[nSize + 63999];

            m_bSnapEnabled = true;
            Step(1);

            Stopwatch sw = new Stopwatch();

            sw.Start();

            while (!m_evtImageSnapped.WaitOne(100))
            {
                //Application.DoEvents();

                if (sw.Elapsed.TotalMilliseconds > nMsMaxWait)
                    break;
            }

            return;
        }

        /// <summary>
        /// The SampleCB is the call back upon receiving each sample.
        /// </summary>
        /// <param name="SampleTime">Specifies the sample time.</param>
        /// <param name="pSample">Specifies the interface used to retrieve a sample.</param>
        /// <returns>Always retunrs 0 for this function is not used.</returns>
        public int SampleCB(double SampleTime, IMediaSample pSample)
        {
            Marshal.ReleaseComObject(pSample);
            return 0;
        }

        /// <summary>
        /// The BufferCB is the callback used to receive buffered video data from the SampleGrabber.
        /// </summary>
        /// <param name="SampleTime">Specifies the sample time.</param>
        /// <param name="pBuffer">Specifies the buffered data.</param>
        /// <param name="BufferLen">Specifies the buffered data length.</param>
        /// <returns>This function returns 0.</returns>
        /// <remarks>
        /// Upon successfully receiving video data and converting it to a Bitmap, the bitmap is then sent
        /// on the the OnSnapshot event handler.
        /// </remarks>
        public int BufferCB(double SampleTime, IntPtr pBuffer, int BufferLen)
        {
            if (pBuffer == IntPtr.Zero || m_rgBuffer == null || BufferLen >= m_rgBuffer.Length || !m_bSnapEnabled)
                return 0;

            if (OnSnapshot != null)
            {
                Marshal.Copy(pBuffer, m_rgBuffer, 0, BufferLen);

                int nWid = m_videoInfoHeader.BmiHeader.Width;
                int nHt = m_videoInfoHeader.BmiHeader.Height;
                int nStride = nWid * 3;

                GCHandle handle = GCHandle.Alloc(m_rgBuffer, GCHandleType.Pinned);
                long nScan0 = handle.AddrOfPinnedObject().ToInt64();
                nScan0 += (nHt - 1) * nStride;

                Bitmap bmp = new Bitmap(nWid, nHt, -nStride, System.Drawing.Imaging.PixelFormat.Format24bppRgb, new IntPtr(nScan0));

                handle.Free();

                if (m_bInvertImage)
                    bmp = invertImage(bmp);

                OnSnapshot(this, new ImageArgs(bmp, m_bInvertImage));
            }

            m_bSnapEnabled = false;
            m_evtImageSnapped.Set();

            return 0;
        }

        private Bitmap invertImage(Bitmap bmp)
        {
            Bitmap bmpNew = new Bitmap(bmp.Width, bmp.Height);
            ColorMatrix colorMatrix = new ColorMatrix(new float[][]
                {
                    new float[] {-1, 0, 0, 0, 0},
                    new float[] {0, -1, 0, 0, 0},
                    new float[] {0, 0, -1, 0, 0},
                    new float[] {0, 0, 0, 1, 0},
                    new float[] {1, 1, 1, 0, 1}
                });
            ImageAttributes attributes = new ImageAttributes();

            attributes.SetColorMatrix(colorMatrix);

            using (Graphics g = Graphics.FromImage(bmpNew))
            {              
                g.DrawImage(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height), 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, attributes);
            }

            bmp.Dispose();

            return bmpNew;
        }
    }

    /// <summary>
    /// The ImageArgs provides the arguments sent to the OnSnapshot event.
    /// </summary>
    public class ImageArgs : EventArgs
    {
        Bitmap m_bmp;
        bool m_bInverted = false;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="bmp">Specifies the bitmap of the image snapshot.</param>
        /// <param name="bInverted">Specifies whether or not the image was inverted (colorwise).</param>
        public ImageArgs(Bitmap bmp, bool bInverted)
        {
            m_bmp = bmp;
            m_bInverted = bInverted;
        }

        /// <summary>
        /// Returns whether or not the colors of the image have been inverted.
        /// </summary>
        public bool Inverted
        {
            get { return m_bInverted; }
        }

        /// <summary>
        /// Returns the bitmap of the snapshot.
        /// </summary>
        public Bitmap Image
        {
            get { return m_bmp; }
        }
    }
}
