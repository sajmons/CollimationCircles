using System;
using System.Runtime.InteropServices;

namespace CollimationCircles.Services
{
    /// <summary>
    /// P/Invoke bindings for libuvc (https://github.com/libuvc/libuvc).
    ///
    /// libuvc is a cross-platform C library that provides full UVC camera access:
    ///   - Kernel driver detachment (libusb_detach_kernel_driver on macOS)
    ///   - UVC descriptor parsing (formats, frames, intervals)
    ///   - Probe/commit negotiation
    ///   - Isochronous &amp; bulk streaming with multiple queued transfer buffers
    ///   - Frame assembly (FID/EOF, MJPEG/YUYV/etc.)
    ///   - All UVC processing-unit and camera-terminal controls
    ///
    /// This replaces the custom IOKit + raw libusb implementation with a tested
    /// library that is already used by astronomy software (oaCapture, etc.) on macOS.
    /// </summary>
    internal static class LibUvc
    {
        private const string LibName = "libuvc";

        // -------------------------------------------------------------------
        // Error codes (enum uvc_error)
        // -------------------------------------------------------------------

        public const int UVC_SUCCESS = 0;
        public const int UVC_ERROR_IO = -1;
        public const int UVC_ERROR_INVALID_PARAM = -2;
        public const int UVC_ERROR_ACCESS = -3;
        public const int UVC_ERROR_NO_DEVICE = -4;
        public const int UVC_ERROR_NOT_FOUND = -5;
        public const int UVC_ERROR_BUSY = -6;
        public const int UVC_ERROR_TIMEOUT = -7;
        public const int UVC_ERROR_OVERFLOW = -8;
        public const int UVC_ERROR_PIPE = -9;
        public const int UVC_ERROR_INTERRUPTED = -10;
        public const int UVC_ERROR_NO_MEM = -11;
        public const int UVC_ERROR_NOT_SUPPORTED = -12;
        public const int UVC_ERROR_INVALID_DEVICE = -50;
        public const int UVC_ERROR_INVALID_MODE = -51;

        // -------------------------------------------------------------------
        // Frame formats (enum uvc_frame_format)
        // -------------------------------------------------------------------

        public const int UVC_FRAME_FORMAT_UNKNOWN = 0;
        public const int UVC_FRAME_FORMAT_ANY = 0;
        public const int UVC_FRAME_FORMAT_UNCOMPRESSED = 1;
        public const int UVC_FRAME_FORMAT_COMPRESSED = 2;
        public const int UVC_FRAME_FORMAT_YUYV = 3;
        public const int UVC_FRAME_FORMAT_UYVY = 4;
        public const int UVC_FRAME_FORMAT_RGB = 5;
        public const int UVC_FRAME_FORMAT_BGR = 6;
        public const int UVC_FRAME_FORMAT_MJPEG = 7;
        public const int UVC_FRAME_FORMAT_H264 = 8;
        public const int UVC_FRAME_FORMAT_GRAY8 = 9;
        public const int UVC_FRAME_FORMAT_GRAY16 = 10;

        // -------------------------------------------------------------------
        // Request codes (enum uvc_req_code)
        // -------------------------------------------------------------------

        public const byte UVC_SET_CUR = 0x01;
        public const byte UVC_GET_CUR = 0x81;
        public const byte UVC_GET_MIN = 0x82;
        public const byte UVC_GET_MAX = 0x83;
        public const byte UVC_GET_RES = 0x84;
        public const byte UVC_GET_LEN = 0x85;
        public const byte UVC_GET_INFO = 0x86;
        public const byte UVC_GET_DEF = 0x87;

        // -------------------------------------------------------------------
        // VS descriptor subtypes (enum uvc_vs_desc_subtype)
        // -------------------------------------------------------------------

        public const int UVC_VS_FORMAT_UNCOMPRESSED = 0x04;
        public const int UVC_VS_FRAME_UNCOMPRESSED = 0x05;
        public const int UVC_VS_FORMAT_MJPEG = 0x06;
        public const int UVC_VS_FRAME_MJPEG = 0x07;

        // -------------------------------------------------------------------
        // Structures
        // -------------------------------------------------------------------

        /// <summary>
        /// uvc_stream_ctrl_t — 34 bytes matching the UVC probe/commit structure.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct uvc_stream_ctrl_t
        {
            public ushort bmHint;
            public byte bFormatIndex;
            public byte bFrameIndex;
            public uint dwFrameInterval;
            public ushort wKeyFrameRate;
            public ushort wPFrameRate;
            public ushort wCompQuality;
            public ushort wCompWindowSize;
            public ushort wDelay;
            public uint dwMaxVideoFrameSize;
            public uint dwMaxPayloadTransferSize;
            public uint dwClockFrequency;
            public byte bmFramingInfo;
            public byte bPreferredVersion;
            public byte bMinVersion;
            public byte bMaxVersion;
            public byte bInterfaceNumber;
        }

        /// <summary>
        /// uvc_frame_t — the frame delivered to the callback.
        /// We only need the first few fields; the rest are accessed via Marshal.
        /// Layout: data(ptr) data_bytes(size) width(uint32) height(uint32) frame_format(int) ...
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct uvc_frame_t
        {
            public IntPtr data;            // void *data
            public nuint data_bytes;       // size_t data_bytes
            public uint width;             // uint32_t width
            public uint height;            // uint32_t height
            public int frame_format;       // enum uvc_frame_format
            public nuint step;             // size_t step
            public uint sequence;          // uint32_t sequence
            // capture_time (struct timeval) and remaining fields follow
            // but we don't need them — we read data/data_bytes/width/height only.
        }

        // The linked-list structs use prev/next pointers (utlist DL_FOREACH).
        // We access fields by offset via Marshal instead of full structs.

        // uvc_format_desc_t layout (after IUNKNOWN-style headers in the linked list):
        //   parent(ptr)  prev(ptr)  next(ptr)
        //   bDescriptorSubtype(enum=int)  bFormatIndex(byte)  bNumFrameDescriptors(byte)
        //   guidFormat[16] / fourccFormat[4] (union, 16 bytes)
        //   bBitsPerPixel / bmFlags (union, 1 byte)
        //   bDefaultFrameIndex(byte)
        //   ... more fields
        // We read bFormatIndex and fourccFormat by offset.

        // uvc_frame_desc_t layout:
        //   parent(ptr)  prev(ptr)  next(ptr)
        //   bDescriptorSubtype(enum=int)  bFrameIndex(byte)  bmCapabilities(byte)
        //   wWidth(ushort)  wHeight(ushort)
        //   dwMinBitRate(uint)  dwMaxBitRate(uint)
        //   dwMaxVideoFrameBufferSize(uint)  dwDefaultFrameInterval(uint)
        //   ... more fields
        // We read wWidth, wHeight, bFrameIndex by offset.

        // uvc_processing_unit_t layout:
        //   prev(ptr)  next(ptr)  bUnitID(byte)  bSourceID(byte)  bmControls(uint64)
        // We read bUnitID and bmControls by offset.

        // uvc_input_terminal_t layout:
        //   prev(ptr)  next(ptr)  bTerminalID(byte)  wTerminalType(enum=int)
        //   wObjectiveFocalLengthMin(ushort)  wObjectiveFocalLengthMax(ushort)
        //   wOcularFocalLength(ushort)  bmControls(uint64)
        // We read bTerminalID and bmControls by offset.

        // -------------------------------------------------------------------
        // Callback delegate
        // -------------------------------------------------------------------

        /// <summary>
        /// uvc_frame_callback_t: void callback(uvc_frame_t *frame, void *user_ptr)
        /// Called by libuvc on the streaming thread when a complete frame is ready.
        /// WARNING: Must not call any uvc_* functions from within the callback.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void uvc_frame_callback_t(IntPtr frame, IntPtr user_ptr);

        // -------------------------------------------------------------------
        // Core API
        // -------------------------------------------------------------------

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_init(out IntPtr ctx, IntPtr usb_ctx);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void uvc_exit(IntPtr ctx);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_find_device(IntPtr ctx, out IntPtr dev, int vid, int pid,
            [MarshalAs(UnmanagedType.LPStr)] string? sn);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_open(IntPtr dev, out IntPtr devh);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void uvc_close(IntPtr devh);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void uvc_unref_device(IntPtr dev);

        // -------------------------------------------------------------------
        // Device descriptor
        // -------------------------------------------------------------------

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_device_descriptor(IntPtr dev, out IntPtr desc);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void uvc_free_device_descriptor(IntPtr desc);

        // uvc_device_descriptor_t: idVendor(ushort) idProduct(ushort) bcdUVC(ushort)
        //   serialNumber(ptr) manufacturer(ptr) product(ptr)
        [StructLayout(LayoutKind.Sequential)]
        public struct uvc_device_descriptor_t
        {
            public ushort idVendor;
            public ushort idProduct;
            public ushort bcdUVC;
            public IntPtr serialNumber;
            public IntPtr manufacturer;
            public IntPtr product;
        }

        // -------------------------------------------------------------------
        // Format / frame descriptor enumeration
        // -------------------------------------------------------------------

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr uvc_get_format_descs(IntPtr devh);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr uvc_get_processing_units(IntPtr devh);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr uvc_get_camera_terminal(IntPtr devh);

        // -------------------------------------------------------------------
        // Stream control
        // -------------------------------------------------------------------

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_stream_ctrl_format_size(
            IntPtr devh, ref uvc_stream_ctrl_t ctrl,
            int format, int width, int height, int fps);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_probe_stream_ctrl(IntPtr devh, ref uvc_stream_ctrl_t ctrl);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_start_streaming(
            IntPtr devh, ref uvc_stream_ctrl_t ctrl,
            uvc_frame_callback_t cb, IntPtr user_ptr, byte flags);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void uvc_stop_streaming(IntPtr devh);

        // -------------------------------------------------------------------
        // Frame allocation
        // -------------------------------------------------------------------

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr uvc_allocate_frame(nuint data_bytes);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern void uvc_free_frame(IntPtr frame);

        // -------------------------------------------------------------------
        // Generic control get/set (for arbitrary unit/selector)
        // -------------------------------------------------------------------

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_ctrl(IntPtr devh, byte unit, byte ctrl,
            byte[] data, int len, byte req_code);

        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_set_ctrl(IntPtr devh, byte unit, byte ctrl,
            byte[] data, int len);

        // -------------------------------------------------------------------
        // Typed control getters/setters
        // -------------------------------------------------------------------

        // Brightness (int16, signed)
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_brightness(IntPtr devh, out short val, byte req);
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_set_brightness(IntPtr devh, short val);

        // Contrast (uint16)
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_contrast(IntPtr devh, out ushort val, byte req);
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_set_contrast(IntPtr devh, ushort val);

        // Contrast auto (uint8)
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_contrast_auto(IntPtr devh, out byte val, byte req);
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_set_contrast_auto(IntPtr devh, byte val);

        // Gain (uint16)
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_gain(IntPtr devh, out ushort val, byte req);
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_set_gain(IntPtr devh, ushort val);

        // Hue (int16, signed)
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_hue(IntPtr devh, out short val, byte req);
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_set_hue(IntPtr devh, short val);

        // Hue auto (uint8)
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_hue_auto(IntPtr devh, out byte val, byte req);
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_set_hue_auto(IntPtr devh, byte val);

        // Saturation (uint16)
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_saturation(IntPtr devh, out ushort val, byte req);
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_set_saturation(IntPtr devh, ushort val);

        // Sharpness (uint16)
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_sharpness(IntPtr devh, out ushort val, byte req);
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_set_sharpness(IntPtr devh, ushort val);

        // Gamma (uint16)
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_gamma(IntPtr devh, out ushort val, byte req);
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_set_gamma(IntPtr devh, ushort val);

        // White balance temperature (uint16)
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_white_balance_temperature(IntPtr devh, out ushort val, byte req);
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_set_white_balance_temperature(IntPtr devh, ushort val);

        // White balance temperature auto (uint8)
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_white_balance_temperature_auto(IntPtr devh, out byte val, byte req);
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_set_white_balance_temperature_auto(IntPtr devh, byte val);

        // Backlight compensation (uint16)
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_backlight_compensation(IntPtr devh, out ushort val, byte req);
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_set_backlight_compensation(IntPtr devh, ushort val);

        // Power line frequency (uint8)
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_power_line_frequency(IntPtr devh, out byte val, byte req);
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_set_power_line_frequency(IntPtr devh, byte val);

        // AE mode (uint8)
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_ae_mode(IntPtr devh, out byte val, byte req);
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_set_ae_mode(IntPtr devh, byte val);

        // Exposure absolute (uint32)
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_exposure_abs(IntPtr devh, out uint val, byte req);
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_set_exposure_abs(IntPtr devh, uint val);

        // Focus absolute (uint16)
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_focus_abs(IntPtr devh, out ushort val, byte req);
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_set_focus_abs(IntPtr devh, ushort val);

        // Focus auto (uint8)
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_focus_auto(IntPtr devh, out byte val, byte req);
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_set_focus_auto(IntPtr devh, byte val);

        // Zoom absolute (uint16)
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_zoom_abs(IntPtr devh, out ushort val, byte req);
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_set_zoom_abs(IntPtr devh, ushort val);

        // Iris absolute (uint16)
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_iris_abs(IntPtr devh, out ushort val, byte req);
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_set_iris_abs(IntPtr devh, ushort val);

        // Roll absolute (int16, signed)
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_get_roll_abs(IntPtr devh, out short val, byte req);
        [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
        public static extern int uvc_set_roll_abs(IntPtr devh, short val);

        // -------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------

        public static string ErrorName(int error)
        {
            return error switch
            {
                UVC_SUCCESS => "SUCCESS",
                UVC_ERROR_IO => "ERROR_IO",
                UVC_ERROR_INVALID_PARAM => "ERROR_INVALID_PARAM",
                UVC_ERROR_ACCESS => "ERROR_ACCESS",
                UVC_ERROR_NO_DEVICE => "ERROR_NO_DEVICE",
                UVC_ERROR_NOT_FOUND => "ERROR_NOT_FOUND",
                UVC_ERROR_BUSY => "ERROR_BUSY",
                UVC_ERROR_TIMEOUT => "ERROR_TIMEOUT",
                UVC_ERROR_OVERFLOW => "ERROR_OVERFLOW",
                UVC_ERROR_PIPE => "ERROR_PIPE",
                UVC_ERROR_INTERRUPTED => "ERROR_INTERRUPTED",
                UVC_ERROR_NO_MEM => "ERROR_NO_MEM",
                UVC_ERROR_NOT_SUPPORTED => "ERROR_NOT_SUPPORTED",
                UVC_ERROR_INVALID_DEVICE => "ERROR_INVALID_DEVICE",
                UVC_ERROR_INVALID_MODE => "ERROR_INVALID_MODE",
                _ => $"UNKNOWN({error})"
            };
        }
    }
}