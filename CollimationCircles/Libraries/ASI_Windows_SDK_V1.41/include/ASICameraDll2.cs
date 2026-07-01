using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ZWOptical.ASISDK
{
    public class ASICameraDll2
    {
        public enum ASI_CONTROL_TYPE
        {
            ASI_GAIN = 0,
            ASI_EXPOSURE,
            ASI_GAMMA,
            ASI_WB_R,
            ASI_WB_B,
            ASI_BRIGHTNESS,
            ASI_BANDWIDTHOVERLOAD,
            ASI_OVERCLOCK,
            ASI_TEMPERATURE,// return 10*temperature
            ASI_FLIP,
            ASI_AUTO_MAX_GAIN,
            ASI_AUTO_MAX_EXP,
            ASI_AUTO_MAX_BRIGHTNESS,
            ASI_HARDWARE_BIN,
            ASI_HIGH_SPEED_MODE,
            ASI_COOLER_POWER_PERC,
            ASI_TARGET_TEMP,// not need *10
            ASI_COOLER_ON,
            ASI_MONO_BIN,
            ASI_FAN_ON,
            ASI_PATTERN_ADJUST,
            ASI_ANTI_DEW_HEATER,
            ASI_HUMIDITY,
            ASI_ENABLE_DDR
        }


        public enum ASI_IMG_TYPE
        {
            //Supported image type
            ASI_IMG_RAW8 = 0,
            ASI_IMG_RGB24,
            ASI_IMG_RAW16,
            ASI_IMG_Y8,
            ASI_IMG_END = -1
        }


        public enum ASI_GUIDE_DIRECTION
        {
            ASI_GUIDE_NORTH = 0,
            ASI_GUIDE_SOUTH,
            ASI_GUIDE_EAST,
            ASI_GUIDE_WEST
        }

        public enum ASI_BAYER_PATTERN
        {
            ASI_BAYER_RG = 0,
            ASI_BAYER_BG,
            ASI_BAYER_GR,
            ASI_BAYER_GB
        };

        public enum ASI_EXPOSURE_STATUS
        {
            ASI_EXP_IDLE = 0,//: idle states, you can start exposure now
            ASI_EXP_WORKING,//: exposing
            ASI_EXP_SUCCESS,// exposure finished and waiting for download
            ASI_EXP_FAILED,//:exposure failed, you need to start exposure again
        };

        public enum ASI_ERROR_CODE
        { //ASI ERROR CODE
            ASI_SUCCESS = 0,
            ASI_ERROR_INVALID_INDEX, //no camera connected or index value out of boundary
            ASI_ERROR_INVALID_ID, //invalid ID
            ASI_ERROR_INVALID_CONTROL_TYPE, //invalid control type
            ASI_ERROR_CAMERA_CLOSED, //camera didn't open
            ASI_ERROR_CAMERA_REMOVED, //failed to find the camera, maybe the camera has been removed
            ASI_ERROR_INVALID_PATH, //cannot find the path of the file
            ASI_ERROR_INVALID_FILEFORMAT,
            ASI_ERROR_INVALID_SIZE, //wrong video format size
            ASI_ERROR_INVALID_IMGTYPE, //unsupported image formate
            ASI_ERROR_OUTOF_BOUNDARY, //the startpos is out of boundary
            ASI_ERROR_TIMEOUT, //timeout
            ASI_ERROR_INVALID_SEQUENCE,//stop capture first
            ASI_ERROR_BUFFER_TOO_SMALL, //buffer size is not big enough
            ASI_ERROR_VIDEO_MODE_ACTIVE,
            ASI_ERROR_EXPOSURE_IN_PROGRESS,
            ASI_ERROR_GENERAL_ERROR,//general error, eg: value is out of valid range
            ASI_ERROR_END
        };
        public enum ASI_BOOL
        {
            ASI_FALSE = 0,
            ASI_TRUE
        };
        public enum ASI_FLIP_STATUS
        {
            ASI_FLIP_NONE = 0,//: original
            ASI_FLIP_HORIZ,//: horizontal flip
            ASI_FLIP_VERT,// vertical flip
            ASI_FLIP_BOTH,//:both horizontal and vertical flip

        };
        public struct ASI_CAMERA_INFO
        {
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 64)]
            public byte[] name;// char[64]; //the name of the camera, you can display this to the UI
            public int CameraID; //this is used to control everything of the camera in other functions
            public int MaxHeight; //the max height of the camera
            public int MaxWidth;	//the max width of the camera

            public ASI_BOOL IsColorCam;
            public ASI_BAYER_PATTERN BayerPattern;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public int[] SupportedBins;// int[16]; //1 means bin1 which is supported by every camera, 2 means bin 2 etc.. 0 is the end of supported binning method

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public ASI_IMG_TYPE[] SupportedVideoFormat;// ASI_IMG_TYPE[8]; //this array will content with the support output format type.IMG_END is the end of supported video format

            public double PixelSize; //the pixel size of the camera, unit is um. such like 5.6um
            public ASI_BOOL MechanicalShutter;
            public ASI_BOOL ST4Port;
            public ASI_BOOL IsCoolerCam;
            public ASI_BOOL IsUSB3Host;
            public ASI_BOOL IsUSB3Camera;
            public float ElecPerADU;

            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 24)]
            public byte[] Unused;//[20];

            public string Name
            {
                get { return Encoding.ASCII.GetString(name).TrimEnd((Char)0); }
            }
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct ASI_CONTROL_CAPS
        {
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 64)]
            public byte[] name; //the name of the Control like Exposure, Gain etc..
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 128)]
            public byte[] description; //description of this control
            public int MaxValue;
            public int MinValue;
            public int DefaultValue;
            public ASI_BOOL IsAutoSupported; //support auto set 1, don't support 0
            public ASI_BOOL IsWritable; //some control like temperature can only be read by some cameras 
            public ASI_CONTROL_TYPE ControlType;//this is used to get value and set value of the control
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 32)]
            public byte[] Unused;//[32];

  			public string Name
            {
                get { return Encoding.ASCII.GetString(name).TrimEnd((Char)0); }
            }

            public string Description
            {
                get { return Encoding.ASCII.GetString(description).TrimEnd((Char)0); }
            }
        }      

        
        public struct ASI_ID{
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 8)]
	        public byte[] id;
            public string ID
            {
                get { return Encoding.ASCII.GetString(id).TrimEnd((Char)0); }
            }
        }

        [DllImport("ASICamera2.dll", EntryPoint = "ASIGetNumOfConnectedCameras", CallingConvention = CallingConvention.Cdecl)]
        private static extern int ASIGetNumOfConnectedCameras32();

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASIGetNumOfConnectedCameras", CallingConvention = CallingConvention.Cdecl)]
        private static extern int ASIGetNumOfConnectedCameras64();


        [DllImport("ASICamera2.dll", EntryPoint = "ASIGetCameraProperty", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIGetCameraProperty32(out ASI_CAMERA_INFO pASICameraInfo, int iCameraIndex);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASIGetCameraProperty", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIGetCameraProperty64(out ASI_CAMERA_INFO pASICameraInfo, int iCameraIndex);


        [DllImport("ASICamera2.dll", EntryPoint = "ASIOpenCamera", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIOpenCamera32(int iCameraID);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASIOpenCamera", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIOpenCamera64(int iCameraID);

        [DllImport("ASICamera2.dll", EntryPoint = "ASIInitCamera", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIInitCamera32(int iCameraID);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASIInitCamera", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIInitCamera64(int iCameraID);



        [DllImport("ASICamera2.dll", EntryPoint = "ASICloseCamera", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASICloseCamera32(int iCameraID);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASICloseCamera", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASICloseCamera64(int iCameraID);


        [DllImport("ASICamera2.dll", EntryPoint = "ASIGetNumOfControls", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIGetNumOfControls32(int iCameraID, out int piNumberOfControls);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASIGetNumOfControls", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIGetNumOfControls64(int iCameraID, out int piNumberOfControls);


        [DllImport("ASICamera2.dll", EntryPoint = "ASIGetControlCaps", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIGetControlCaps32(int iCameraID, int iControlIndex, out ASI_CONTROL_CAPS pControlCaps);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASIGetControlCaps", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIGetControlCaps64(int iCameraID, int iControlIndex, out ASI_CONTROL_CAPS pControlCaps);


        [DllImport("ASICamera2.dll", EntryPoint = "ASISetControlValue", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASISetControlValue32(int iCameraID, ASI_CONTROL_TYPE ControlType, int lValue, ASI_BOOL bAuto);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASISetControlValue", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASISetControlValue64(int iCameraID, ASI_CONTROL_TYPE ControlType, int lValue, ASI_BOOL bAuto);


        [DllImport("ASICamera2.dll", EntryPoint = "ASIGetControlValue", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIGetControlValue32(int iCameraID, ASI_CONTROL_TYPE ControlType, out int plValue, out ASI_BOOL pbAuto);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASIGetControlValue", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIGetControlValue64(int iCameraID, ASI_CONTROL_TYPE ControlType, out int plValue, out ASI_BOOL pbAuto);


        [DllImport("ASICamera2.dll", EntryPoint = "ASISetROIFormat", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASISetROIFormat32(int iCameraID, int iWidth, int iHeight, int iBin, ASI_IMG_TYPE Img_type);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASISetROIFormat", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASISetROIFormat64(int iCameraID, int iWidth, int iHeight, int iBin, ASI_IMG_TYPE Img_type);


        [DllImport("ASICamera2.dll", EntryPoint = "ASIGetROIFormat", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIGetROIFormat32(int iCameraID, out int piWidth, out int piHeight, out int piBin, out ASI_IMG_TYPE pImg_type);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASIGetROIFormat", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIGetROIFormat64(int iCameraID, out int piWidth, out int piHeight, out int piBin, out ASI_IMG_TYPE pImg_type);


        [DllImport("ASICamera2.dll", EntryPoint = "ASISetStartPos", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASISetStartPos32(int iCameraID, int iStartX, int iStartY);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASISetStartPos", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASISetStartPos64(int iCameraID, int iStartX, int iStartY);


        [DllImport("ASICamera2.dll", EntryPoint = "ASIGetStartPos", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIGetStartPos32(int iCameraID, out int piStartX, out int piStartY);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASIGetStartPos", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIGetStartPos64(int iCameraID, out int piStartX, out int piStartY);


        [DllImport("ASICamera2.dll", EntryPoint = "ASIStartVideoCapture", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIStartVideoCapture32(int iCameraID);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASIStartVideoCapture", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIStartVideoCapture64(int iCameraID);


        [DllImport("ASICamera2.dll", EntryPoint = "ASIStopVideoCapture", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIStopVideoCapture32(int iCameraID);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASIStopVideoCapture", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIStopVideoCapture64(int iCameraID);


        [DllImport("ASICamera2.dll", EntryPoint = "ASIGetVideoData", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIGetVideoData32(int iCameraID, IntPtr pBuffer, int lBuffSize, int iWaitms);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASIGetVideoData", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIGetVideoData64(int iCameraID, IntPtr pBuffer, int lBuffSize, int iWaitms);


        [DllImport("ASICamera2.dll", EntryPoint = "ASIPulseGuideOn", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIPulseGuideOn32(int iCameraID, ASI_GUIDE_DIRECTION direction);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASIPulseGuideOn", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIPulseGuideOn64(int iCameraID, ASI_GUIDE_DIRECTION direction);


        [DllImport("ASICamera2.dll", EntryPoint = "ASIPulseGuideOff", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIPulseGuideOff32(int iCameraID, ASI_GUIDE_DIRECTION direction);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASIPulseGuideOff", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIPulseGuideOff64(int iCameraID, ASI_GUIDE_DIRECTION direction);


        [DllImport("ASICamera2.dll", EntryPoint = "ASIStartExposure", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIStartExposure32(int iCameraID, ASI_BOOL bIsDark);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASIStartExposure", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIStartExposure64(int iCameraID, ASI_BOOL bIsDark);


        [DllImport("ASICamera2.dll", EntryPoint = "ASIStopExposure", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIStopExposure32(int iCameraID);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASIStopExposure", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIStopExposure64(int iCameraID);


        [DllImport("ASICamera2.dll", EntryPoint = "ASIGetExpStatus", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIGetExpStatus32(int iCameraID, out ASI_EXPOSURE_STATUS pExpStatus);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASIGetExpStatus", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIGetExpStatus64(int iCameraID, out ASI_EXPOSURE_STATUS pExpStatus);


        [DllImport("ASICamera2.dll", EntryPoint = "ASIGetDataAfterExp", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIGetDataAfterExp32(int iCameraID, IntPtr pBuffer, int lBuffSize);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASIGetDataAfterExp", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIGetDataAfterExp64(int iCameraID, IntPtr pBuffer, int lBuffSize);


        [DllImport("ASICamera2.dll", EntryPoint = "ASIGetGainOffset", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIGetGainOffset32(int iCameraID, out int Offset_HighestDR, out int Offset_UnityGain, out int Gain_LowestRN, out int Offset_LowestRN);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASIGetGainOffset", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIGetGainOffset64(int iCameraID, out int Offset_HighestDR, out int Offset_UnityGain, out int Gain_LowestRN, out int Offset_LowestRN);

        [DllImport("ASICamera2.dll", EntryPoint = "ASIGetID", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIGetID32(int iCameraID, out ASI_ID pID);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASIGetID", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASIGetID64(int iCameraID, out ASI_ID pID);

        [DllImport("ASICamera2.dll", EntryPoint = "ASISetID", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASISetID32(int iCameraID, ASI_ID ID);

        [DllImport("ASICamera2_x64.dll", EntryPoint = "ASISetID", CallingConvention = CallingConvention.Cdecl)]
        private static extern ASI_ERROR_CODE ASISetID64(int iCameraID, ASI_ID ID);



        public static int ASIGetNumOfConnectedCameras() { return IntPtr.Size == 8 /* 64bit */ ? ASIGetNumOfConnectedCameras64() : ASIGetNumOfConnectedCameras32(); }
      
        public static ASI_ERROR_CODE ASIGetCameraProperty(out ASI_CAMERA_INFO pASICameraInfo, int iCameraIndex)
        { return IntPtr.Size == 8 /* 64bit */ ? ASIGetCameraProperty64(out pASICameraInfo, iCameraIndex) : ASIGetCameraProperty32(out pASICameraInfo, iCameraIndex); }

        public static ASI_ERROR_CODE ASIOpenCamera(int iCameraID)
        { return IntPtr.Size == 8 /* 64bit */ ? ASIOpenCamera64(iCameraID) : ASIOpenCamera32(iCameraID); }

        public static ASI_ERROR_CODE ASIInitCamera(int iCameraID)
        { return IntPtr.Size == 8 /* 64bit */ ? ASIInitCamera64(iCameraID) : ASIInitCamera32(iCameraID); }

        public static ASI_ERROR_CODE ASICloseCamera(int iCameraID)
        { return IntPtr.Size == 8 /* 64bit */ ? ASICloseCamera64(iCameraID) : ASICloseCamera32(iCameraID); }

        public static ASI_ERROR_CODE ASIGetNumOfControls(int iCameraID, out int piNumberOfControls)
        { return IntPtr.Size == 8 /* 64bit */ ? ASIGetNumOfControls64(iCameraID, out piNumberOfControls) : ASIGetNumOfControls32(iCameraID, out piNumberOfControls); }

        public static ASI_ERROR_CODE ASIGetControlCaps(int iCameraID, int iControlIndex, out ASI_CONTROL_CAPS pControlCaps)
        { return IntPtr.Size == 8 /* 64bit */ ? ASIGetControlCaps64(iCameraID, iControlIndex, out pControlCaps) : ASIGetControlCaps32(iCameraID, iControlIndex, out pControlCaps); }

        public static ASI_ERROR_CODE ASISetControlValue(int iCameraID, ASI_CONTROL_TYPE ControlType, int lValue)
        { return IntPtr.Size == 8 /* 64bit */ ? ASISetControlValue64(iCameraID, ControlType, lValue, ASI_BOOL.ASI_FALSE) : ASISetControlValue32(iCameraID, ControlType, lValue, ASI_BOOL.ASI_FALSE); }

        public static int ASIGetControlValue(int iCameraID, ASI_CONTROL_TYPE ControlType)
        {
            ASI_BOOL pbAuto;
            int plValue;
            ASI_ERROR_CODE err = IntPtr.Size == 8 /* 64bit */ ? ASIGetControlValue64(iCameraID, ControlType, out plValue, out pbAuto) : ASIGetControlValue32(iCameraID, ControlType, out plValue, out pbAuto);
            return plValue;
        }

        public static ASI_ERROR_CODE ASISetROIFormat(int iCameraID, int iWidth, int iHeight, int iBin, ASI_IMG_TYPE Img_type)
        { return IntPtr.Size == 8 /* 64bit */ ? ASISetROIFormat64(iCameraID, iWidth, iHeight, iBin, Img_type) : ASISetROIFormat32(iCameraID, iWidth, iHeight, iBin, Img_type); }

        public static ASI_ERROR_CODE ASIGetROIFormat(int iCameraID, out int piWidth, out int piHeight, out int piBin, out ASI_IMG_TYPE pImg_type)
        { return IntPtr.Size == 8 /* 64bit */ ? ASIGetROIFormat64(iCameraID, out piWidth, out piHeight, out piBin, out pImg_type) : ASIGetROIFormat32(iCameraID, out piWidth, out piHeight, out piBin, out pImg_type); }

        public static ASI_ERROR_CODE ASISetStartPos(int iCameraID, int iStartX, int iStartY)
        { return IntPtr.Size == 8 /* 64bit */ ? ASISetStartPos64(iCameraID, iStartX, iStartY) : ASISetStartPos32(iCameraID, iStartX, iStartY); }

        public static ASI_ERROR_CODE ASIGetStartPos(int iCameraID, out int piStartX, out int piStartY)
        { return IntPtr.Size == 8 /* 64bit */ ? ASIGetStartPos64(iCameraID, out piStartX, out piStartY) : ASIGetStartPos32(iCameraID, out piStartX, out piStartY); }

        public static ASI_ERROR_CODE ASIStartVideoCapture(int iCameraID)
        { return IntPtr.Size == 8 /* 64bit */ ? ASIStartVideoCapture64(iCameraID) : ASIStartVideoCapture32(iCameraID); }

        public static ASI_ERROR_CODE ASIStopVideoCapture(int iCameraID)
        { return IntPtr.Size == 8 /* 64bit */ ? ASIStopVideoCapture64(iCameraID) : ASIStopVideoCapture32(iCameraID); }

        public static ASI_ERROR_CODE ASIGetVideoData(int iCameraID, IntPtr pBuffer, int lBuffSize, int iWaitms)
        { return IntPtr.Size == 8 /* 64bit */ ? ASIGetVideoData64(iCameraID, pBuffer, lBuffSize, iWaitms) : ASIGetVideoData32(iCameraID, pBuffer, lBuffSize, iWaitms); }

        public static ASI_ERROR_CODE ASIPulseGuideOn(int iCameraID, ASI_GUIDE_DIRECTION direction)
        { return IntPtr.Size == 8 /* 64bit */ ? ASIPulseGuideOn64(iCameraID, direction) : ASIPulseGuideOn32(iCameraID, direction); }

        public static ASI_ERROR_CODE ASIPulseGuideOff(int iCameraID, ASI_GUIDE_DIRECTION direction)
        { return IntPtr.Size == 8 /* 64bit */ ? ASIPulseGuideOff64(iCameraID, direction) : ASIPulseGuideOff32(iCameraID, direction); }

        public static ASI_ERROR_CODE ASIStartExposure(int iCameraID, ASI_BOOL bIsDark)
        { return IntPtr.Size == 8 /* 64bit */ ? ASIStartExposure64(iCameraID, bIsDark) : ASIStartExposure32(iCameraID, bIsDark); }

        public static ASI_ERROR_CODE ASIStopExposure(int iCameraID)
        { return IntPtr.Size == 8 /* 64bit */ ? ASIStopExposure64(iCameraID) : ASIStopExposure32(iCameraID); }

        public static ASI_ERROR_CODE ASIGetExpStatus(int iCameraID, out ASI_EXPOSURE_STATUS pExpStatus)
        { return IntPtr.Size == 8 /* 64bit */ ? ASIGetExpStatus64(iCameraID, out pExpStatus) : ASIGetExpStatus32(iCameraID, out pExpStatus); }

        public static ASI_ERROR_CODE ASIGetDataAfterExp(int iCameraID, IntPtr pBuffer, int lBuffSize)
        { return IntPtr.Size == 8 /* 64bit */ ? ASIGetDataAfterExp64(iCameraID, pBuffer, lBuffSize) : ASIGetDataAfterExp32(iCameraID, pBuffer, lBuffSize); }

        public static ASI_ERROR_CODE ASIGetGainOffset(int iCameraID, out int Offset_HighestDR, out int Offset_UnityGain, out int Gain_LowestRN, out int Offset_LowestRN)
        { return IntPtr.Size == 8 /* 64bit */ ? ASIGetGainOffset64(iCameraID, out Offset_HighestDR, out Offset_UnityGain, out Gain_LowestRN, out Offset_LowestRN) : ASIGetGainOffset32(iCameraID, out Offset_HighestDR, out Offset_UnityGain, out Gain_LowestRN, out Offset_LowestRN); }


        public static ASI_ERROR_CODE ASIGetID(int iCameraID, out ASI_ID pID)
        { return IntPtr.Size == 8 /* 64bit */ ? ASIGetID64(iCameraID, out pID) : ASIGetID32(iCameraID, out pID); }

        public static ASI_ERROR_CODE ASISetID(int iCameraID, ASI_ID ID)
        { return IntPtr.Size == 8 /* 64bit */ ? ASISetID64(iCameraID, ID) : ASISetID32(iCameraID, ID); }

    }


}
