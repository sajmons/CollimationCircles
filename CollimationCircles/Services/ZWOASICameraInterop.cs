using System;
using System.Runtime.InteropServices;

namespace CollimationCircles.Services
{
    /// <summary>
    /// P/Invoke wrapper for ZWO ASI Camera SDK (libASICamera2)
    /// This allows direct communication with ZWO astro cameras on macOS, Linux, and Windows
    /// </summary>
    internal static class ZWOASICameraInterop
    {
        // The SDK exports the same entry points on Windows and macOS.
        private const string LibraryName = "ASICamera2";

        // Error codes
        public enum ASI_ERROR_CODE
        {
            ASI_SUCCESS = 0,
            ASI_ERROR_INVALID_INDEX = 1,
            ASI_ERROR_INVALID_ID = 2,
            ASI_ERROR_INVALID_CONTROL_TYPE = 3,
            ASI_ERROR_CAMERA_CLOSED = 4,
            ASI_ERROR_CAMERA_REMOVED = 5,
            // ... more error codes
        }

        // Control types
        public enum ASI_CONTROL_TYPE
        {
            ASI_GAIN = 0,
            ASI_EXPOSURE = 1,
            ASI_GAMMA = 2,
            ASI_WB_R = 3,
            ASI_WB_B = 4,
            ASI_OFFSET = 5,
            ASI_BANDWIDTHOVERLOAD = 6,
            ASI_OVERCLOCK = 7,
            ASI_TEMPERATURE = 8,
            ASI_FLIP = 9,
            ASI_AUTO_MAX_GAIN = 10,
            ASI_AUTO_MAX_EXP = 11,
            ASI_AUTO_TARGET_BRIGHTNESS = 12,
            ASI_HARDWARE_BIN = 13,
            ASI_HIGH_SPEED_MODE = 14,
            ASI_COOLER_POWER_PERC = 15,
            ASI_TARGET_TEMP = 16,
            ASI_COOLER_ON = 17,
            ASI_MONO_BIN = 18,
            ASI_FAN_ON = 19,
            ASI_PATTERN_ADJUST = 20,
            ASI_ANTI_DEW_HEATER = 21,
            ASI_FAN_ADJUST = 22,
            ASI_PWRLED_BRIGNT = 23,
            ASI_USBHUB_RESET = 24,
            ASI_GPS_SUPPORT = 25,
            ASI_GPS_START_LINE = 26,
            ASI_GPS_END_LINE = 27,
            ASI_ROLLING_INTERVAL = 28,
        }

        // Image format types
        public enum ASI_IMG_TYPE
        {
            ASI_IMG_RAW8 = 0,
            ASI_IMG_RGB24 = 1,
            ASI_IMG_RAW16 = 2,
            ASI_IMG_Y8 = 3,
            ASI_IMG_END = -1
        }

        // Camera info structure
        [StructLayout(LayoutKind.Sequential)]
        public struct ASI_CAMERA_INFO
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] Name;
            public int CameraID;
            public long MaxHeight;
            public long MaxWidth;
            public int IsColorCam;
            public int BayerPattern;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public int[] SupportedBins;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public int[] SupportedVideoFormat;
            public double PixelSize;
            public int MechanicalShutter;
            public int ST4Port;
            public int IsCoolerCam;
            public int IsUSB3Host;
            public int IsUSB3Camera;
            public float ElecPerADU;
            public int BitDepth;
            public int IsTriggerCam;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] Unused;
        }

        // Control caps structure
        [StructLayout(LayoutKind.Sequential)]
        public struct ASI_CONTROL_CAPS
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] Name;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
            public byte[] Description;
            public long MaxValue;
            public long MinValue;
            public long DefaultValue;
            public int IsAutoSupported;
            public int IsWritable;
            public int ControlType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] Unused;
        }

        // P/Invoke function declarations
        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ASIGetNumOfConnectedCameras();

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ASIGetCameraProperty(ref ASI_CAMERA_INFO pASICameraInfo, int iCameraIndex);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ASIOpenCamera(int iCameraID);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ASIInitCamera(int iCameraID);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ASICloseCamera(int iCameraID);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ASIGetNumOfControls(int iCameraID, ref int piNumberOfControls);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ASIGetControlCaps(int iCameraID, int iControlIndex, ref ASI_CONTROL_CAPS pControlCaps);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ASIGetControlValue(int iCameraID, int iControlType, ref long plValue, ref int pbAuto);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ASISetControlValue(int iCameraID, int iControlType, long lValue, int bAuto);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ASISetROIFormat(int iCameraID, int iWidth, int iHeight, int iBin, ASI_IMG_TYPE imgType);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ASIStartVideoCapture(int iCameraID);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ASIStopVideoCapture(int iCameraID);

        [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int ASIGetVideoData(int iCameraID, IntPtr pBuffer, int lBuffSize, int iWaitms);
    }
}
