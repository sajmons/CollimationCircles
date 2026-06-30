using System.Collections;

namespace DxWebCam
{
    /// <summary>
    /// The VideoCapabilities describe the various video options such as resolution and frames per second.
    /// </summary>
    public class VideoCapability
    {
        int m_nWidth;
        int m_nHeight;
        int m_nFpsMin;
        int m_nFpsMax;
        int m_nTargetFps = 0;

        /// <summary>
        /// The constructor.
        /// </summary>
        /// <param name="nWid">Specifies the video width.</param>
        /// <param name="nHt">Specifies the video height</param>
        /// <param name="nFpsMin">Specifies the minimum frames per second.</param>
        /// <param name="nFpsMax">Specifies the maximum frames per second.</param>
        /// <param name="nTargetFps">Optionally, specifies a target FPS, or 0 to ignore.</param>
        public VideoCapability(int nWid, int nHt, int nFpsMin, int nFpsMax, int nTargetFps = 0)
        {
            m_nWidth = nWid;
            m_nHeight = nHt;
            m_nFpsMin = nFpsMin;
            m_nFpsMax = nFpsMax;
            m_nTargetFps = nTargetFps;
        }

        /// <summary>
        /// Returns the target FPS.
        /// </summary>
        public int TargetFPS
        {
            get { return m_nTargetFps; }
        }

        /// <summary>
        /// Returns the video width.
        /// </summary>
        public int Width
        {
            get { return m_nWidth; }
        }

        /// <summary>
        /// Returns the video height.
        /// </summary>
        public int Height
        {
            get { return m_nHeight; }
        }

        /// <summary>
        /// Returns the minimum frames per second.
        /// </summary>
        public int FpsMin
        {
            get { return m_nFpsMin; }
        }

        /// <summary>
        /// Returns the maximum frames per second.
        /// </summary>
        public int FpsMax
        {
            get { return m_nFpsMax; }
        }

        /// <summary>
        /// Returns a string rendition of the settings.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return "Width: " + m_nWidth.ToString() + " Height: " + m_nHeight.ToString() + " Fps Min: " + m_nFpsMin.ToString() + " Fps Max: " + m_nFpsMax.ToString();
        }
    }

    /// <summary>
    /// The VideoCapabilitiesCollection contains a list of VideoCapabilities.
    /// </summary>
    public class VideoCapabilityCollection : IEnumerable<VideoCapability>
    {
        List<VideoCapability> m_rgItems = new List<VideoCapability>();

        /// <summary>
        /// The constructor.
        /// </summary>
        public VideoCapabilityCollection()
        {
        }

        /// <summary>
        /// The number of items in the collection.
        /// </summary>
        public int Count
        {
            get { return m_rgItems.Count; }
        }

        /// <summary>
        /// Returns an item at a given index.
        /// </summary>
        /// <param name="nIdx">Specifies the index of the item to retrieve.</param>
        /// <returns>The item at the index is returned.</returns>
        public VideoCapability this[int nIdx]
        {
            get { return m_rgItems[nIdx]; }
        }

        /// <summary>
        /// Add a new item to the collection.
        /// </summary>
        /// <param name="v">Specifies the item to add.</param>
        public void Add(VideoCapability v)
        {
            m_rgItems.Add(v);
        }

        /// <summary>
        /// Retuns a matching video configuration.
        /// </summary>
        /// <param name="nWidth">Specifies the width sought or 0 to ignore.</param>
        /// <param name="nHeight">Specifies the height sought or 0 to ignore.</param>
        /// <param name="nFps">Specifies the desired FPS or 0 to ignore.</param>
        /// <returns>If found the video configuration is returned, or null if not found.</returns>
        public VideoCapability FindMatch(int nWidth, int nHeight, int nFps = 0)
        {
            if (nWidth == 0 && nHeight == 0)
                throw new Exception("You must specify at least a width or height value.");

            IEnumerable<VideoCapability> rg = null;

            if (nHeight == 0)
                rg = m_rgItems.Where(p => p.Width == nWidth).OrderByDescending(p => p.FpsMax);
            else if (nWidth == 0)
                rg = m_rgItems.Where(p => p.Height == nHeight).OrderByDescending(p => p.FpsMax).ToList();
            else
                rg = m_rgItems.Where(p => p.Width == nWidth && p.Height == nHeight).OrderByDescending(p => p.FpsMax).ToList();

            if (nFps > 0)
                rg = rg.Where(p => p.FpsMax >= nFps && p.FpsMin <= nFps);

            List<VideoCapability> rgItems = rg.ToList();
            if (rgItems.Count == 0)
                return null;

            return rgItems[0];
        }

        /// <summary>
        /// Returns the collection enumerator.
        /// </summary>
        /// <returns>The enumerator is returned.</returns>
        public IEnumerator<VideoCapability> GetEnumerator()
        {
            return m_rgItems.GetEnumerator();
        }

        /// <summary>
        /// Returns the collection enumerator.
        /// </summary>
        /// <returns>The enumerator is returned.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_rgItems.GetEnumerator();
        }
    }
}
