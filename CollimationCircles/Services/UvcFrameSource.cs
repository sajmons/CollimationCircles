using CollimationCircles.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CollimationCircles.Services
{
    /// <summary>
    /// Direct UVC camera frame source using the libuvc library
    /// (https://github.com/libuvc/libuvc) via P/Invoke.
    ///
    /// libuvc handles kernel driver detachment, UVC descriptor parsing, probe/commit
    /// negotiation, isochronous/bulk streaming with multiple queued transfer buffers,
    /// frame assembly, and all UVC controls — all in a tested C library used by
    /// astronomy software (oaCapture, etc.) on macOS.
    ///
    /// Frames are delivered via <see cref="FrameReady"/> as JPEG-encoded image data
    /// for direct rendering by <see cref="FrameRenderer"/>.
    /// </summary>
    internal sealed class UvcFrameSource : IUvcFrameSource, IDisposable
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        // libuvc context and device handles
        private IntPtr _ctx = IntPtr.Zero;
        private IntPtr _dev = IntPtr.Zero;
        private IntPtr _devh = IntPtr.Zero;

        // Entity IDs for UVC controls
        private byte _puId = 0;  // processing unit entity ID
        private byte _ctId = 0;  // camera terminal entity ID

        // Stream info
        private int _width = 0;
        private int _height = 0;
        private volatile bool _running;

        // Frame callback delegate — MUST be kept alive (stored as a field) to
        // prevent GC collection while libuvc holds a raw function pointer to it.
        private LibUvc.uvc_frame_callback_t? _frameCallback;

        // Track whether we've logged the "non-MJPEG format" warning
        private bool _loggedNonMjpeg;

        // -------------------------------------------------------------------
        // IUvcFrameSource interface
        // -------------------------------------------------------------------

        public bool IsStreaming => _running && _devh != IntPtr.Zero;
        public int FrameWidth => _width;
        public int FrameHeight => _height;
        public event Action<byte[], int, int>? FrameReady;

        public async Task<bool> StartAsync(Camera camera)
        {
            logger.Info($"UvcFrameSource.StartAsync: begin for '{camera.Name}' (VID={camera.VendorId} PID={camera.ProductId})");
            Stop();

            if (camera.VendorId <= 0 || camera.ProductId <= 0)
            {
                logger.Error($"Cannot start UVC stream: vendor/product IDs not available for '{camera.Name}'");
                return false;
            }

            bool opened = await Task.Run(() => OpenDevice(camera.VendorId, camera.ProductId));
            if (!opened)
            {
                logger.Error($"Failed to open UVC device for '{camera.Name}' (VID={camera.VendorId} PID={camera.ProductId})");
                return false;
            }

            // Discover entity IDs for control routing
            DiscoverEntityIds();
            logger.Info($"UvcFrameSource.StartAsync: PU ID={_puId}, CT ID={_ctId}");

            // Enumerate real UVC controls and update the camera's control list
            try
            {
                var realControls = EnumerateControls(camera);
                if (realControls.Count > 0)
                {
                    camera.Controls = realControls;
                    logger.Info($"Updated camera '{camera.Name}' with {realControls.Count} real UVC controls");
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"Failed to enumerate UVC controls for '{camera.Name}' — keeping placeholder controls");
            }

            // Start streaming
            bool started;
            try
            {
                started = StartStreaming(camera);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Unhandled exception while starting UVC stream for '{camera.Name}'");
                started = false;
            }

            if (!started)
            {
                logger.Error($"Failed to start UVC streaming for '{camera.Name}'");
                CloseDevice();
                return false;
            }

            logger.Info($"UVC stream started for '{camera.Name}' ({_width}×{_height})");
            return true;
        }

        public void Stop()
        {
            if (!_running && _devh == IntPtr.Zero) return;

            _running = false;

            if (_devh != IntPtr.Zero)
            {
                try
                {
                    LibUvc.uvc_stop_streaming(_devh);
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "uvc_stop_streaming threw exception");
                }
            }

            CloseDevice();
            logger.Info("UVC stream stopped");
        }

        // -------------------------------------------------------------------
        // Device open / close
        // -------------------------------------------------------------------

        // Last VID/PID for kernel driver restore on close
        private int _lastVendorId = 0;
        private int _lastProductId = 0;
        private int _restoreConfig = 1;

        private bool OpenDevice(int vendorId, int productId)
        {
            logger.Debug($"OpenDevice: begin VID={vendorId} PID={productId}");
            _lastVendorId = vendorId;
            _lastProductId = productId;

            // Step 1: IOKit SetConfiguration(0) to detach the macOS kernel UVC driver.
            // libuvc's libusb_detach_kernel_driver is NOT sufficient on macOS —
            // the kernel driver is an IOKit service, not a libusb module.
            // SetConfiguration(0) unconfigures the device at the IOKit level,
            // which releases the kernel driver from all interfaces.
            logger.Debug("OpenDevice: step 1 — IOKit SetConfiguration(0) to detach kernel driver");
            int targetConfig = IokitHelper.SetConfiguration(vendorId, productId, 0);
            logger.Debug($"OpenDevice: IokitSetConfiguration(0) returned targetConfig={targetConfig}");
            if (targetConfig == 0)
            {
                logger.Error("IOKit SetConfiguration(0) failed — cannot detach kernel driver");
                return false;
            }
            _restoreConfig = targetConfig;

            // Brief wait for kernel driver to release interfaces before the retry loop
            System.Threading.Thread.Sleep(100);

            // Step 2: libuvc init + find + open with retry loop.
            // On macOS there's a race condition: after IOKit SetConfiguration(0)
            // detaches the kernel driver, the driver can re-attach before
            // libuvc claims the interface. We retry the IOKit detach + uvc_open
            // sequence up to 20 times (matching the old code's strategy).
            int ret = LibUvc.uvc_init(out _ctx, IntPtr.Zero);
            if (ret != LibUvc.UVC_SUCCESS)
            {
                logger.Error($"uvc_init failed: {LibUvc.ErrorName(ret)}");
                IokitHelper.RestoreKernelDriver(vendorId, productId);
                return false;
            }
            logger.Debug($"OpenDevice: uvc_init success, ctx={_ctx}");

            bool opened = false;
            for (int attempt = 0; attempt < 20; attempt++)
            {
                // Re-detach kernel driver on each attempt (the driver may have re-attached)
                if (attempt > 0)
                {
                    logger.Debug($"OpenDevice: attempt {attempt} — re-detaching kernel driver via IOKit SetConfiguration(0)");
                    IokitHelper.SetConfiguration(vendorId, productId, 0);
                    System.Threading.Thread.Sleep(50);
                }

                // Re-find the device (the libusb device list may change after reconfig)
                ret = LibUvc.uvc_find_device(_ctx, out _dev, vendorId, productId, null);
                if (ret != LibUvc.UVC_SUCCESS)
                {
                    logger.Debug($"OpenDevice: uvc_find_device attempt {attempt} failed: {LibUvc.ErrorName(ret)}");
                    continue;
                }

                ret = LibUvc.uvc_open(_dev, out _devh);
                if (ret == LibUvc.UVC_SUCCESS)
                {
                    logger.Info($"uvc_open: SUCCESS on attempt {attempt} — device handle={_devh}");
                    opened = true;
                    break;
                }

                logger.Debug($"OpenDevice: uvc_open attempt {attempt} failed: {LibUvc.ErrorName(ret)}");
                LibUvc.uvc_unref_device(_dev);
                _dev = IntPtr.Zero;
                System.Threading.Thread.Sleep(50);
            }

            if (!opened)
            {
                logger.Error($"uvc_open failed after 20 attempts: {LibUvc.ErrorName(ret)}");
                LibUvc.uvc_exit(_ctx);
                _ctx = IntPtr.Zero;
                IokitHelper.RestoreKernelDriver(vendorId, productId);
                return false;
            }

            return true;
        }

        private void CloseDevice()
        {
            if (_devh != IntPtr.Zero)
            {
                LibUvc.uvc_close(_devh);
                _devh = IntPtr.Zero;
            }

            if (_dev != IntPtr.Zero)
            {
                LibUvc.uvc_unref_device(_dev);
                _dev = IntPtr.Zero;
            }

            if (_ctx != IntPtr.Zero)
            {
                LibUvc.uvc_exit(_ctx);
                _ctx = IntPtr.Zero;
            }

            // Restore the kernel driver so the camera works normally again
            // (e.g. in AVFoundation / FaceTime / other apps)
            if (_lastVendorId > 0 && _lastProductId > 0)
            {
                IokitHelper.RestoreKernelDriver(_lastVendorId, _lastProductId);
            }
        }

        // -------------------------------------------------------------------
        // Entity ID discovery
        // -------------------------------------------------------------------

        private void DiscoverEntityIds()
        {
            // Processing unit
            IntPtr pu = LibUvc.uvc_get_processing_units(_devh);
            if (pu != IntPtr.Zero)
            {
                // uvc_processing_unit_t: prev(ptr) next(ptr) bUnitID(byte) bSourceID(byte) bmControls(uint64)
                int offsetUnitId = IntPtr.Size * 2; // after prev + next
                _puId = Marshal.ReadByte(pu, offsetUnitId);
                logger.Info($"Processing unit ID: {_puId}");
            }
            else
            {
                _puId = 1;
                logger.Warn("No processing unit found — using fallback ID 1");
            }

            // Camera terminal (input terminal)
            IntPtr ct = LibUvc.uvc_get_camera_terminal(_devh);
            if (ct != IntPtr.Zero)
            {
                // uvc_input_terminal_t: prev(ptr) next(ptr) bTerminalID(byte) wTerminalType(enum=int) ...
                int offsetTermId = IntPtr.Size * 2; // after prev + next
                _ctId = Marshal.ReadByte(ct, offsetTermId);
                logger.Info($"Camera terminal ID: {_ctId}");
            }
            else
            {
                _ctId = 1;
                logger.Warn("No camera terminal found — using fallback ID 1");
            }

            if (_puId == _ctId)
            {
                logger.Warn($"DiscoverEntityIds: PU and CT share entity ID {_puId}. Camera-terminal controls will be unreliable on this device.");
            }
        }

        // -------------------------------------------------------------------
        // Control enumeration
        // -------------------------------------------------------------------

        private record ControlDef(string Name, byte Selector, int DataLen, bool IsSigned, string? AutoName);
        private record AutoControlDef(string Name, byte Selector);

        // UVC processing unit control selectors
        private const byte UVC_PU_BRIGHTNESS = 0x02;
        private const byte UVC_PU_CONTRAST = 0x03;
        private const byte UVC_PU_GAIN = 0x04;
        private const byte UVC_PU_POWER_LINE_FREQUENCY = 0x05;
        private const byte UVC_PU_HUE = 0x06;
        private const byte UVC_PU_SATURATION = 0x07;
        private const byte UVC_PU_SHARPNESS = 0x08;
        private const byte UVC_PU_GAMMA = 0x09;
        private const byte UVC_PU_WHITE_BALANCE_TEMP = 0x0a;
        private const byte UVC_PU_WHITE_BALANCE_TEMP_AUTO = 0x0b;
        private const byte UVC_PU_HUE_AUTO = 0x10;
        private const byte UVC_PU_CONTRAST_AUTO = 0x13;
        private const byte UVC_PU_BACKLIGHT_COMPENSATION = 0x01;

        // UVC camera terminal control selectors
        private const byte UVC_CT_AE_MODE = 0x02;
        private const byte UVC_CT_EXPOSURE_TIME_ABS = 0x04;
        private const byte UVC_CT_FOCUS_ABS = 0x06;
        private const byte UVC_CT_FOCUS_AUTO = 0x08;
        private const byte UVC_CT_ZOOM_ABS = 0x0b;
        private const byte UVC_CT_IRIS_ABS = 0x09;
        private const byte UVC_CT_ROLL_ABS = 0x0f;

        private static readonly ControlDef[] PuControls =
        [
            new("Brightness",           UVC_PU_BRIGHTNESS,             2, true,  null),
            new("Contrast",             UVC_PU_CONTRAST,               2, false, "ContrastAuto"),
            new("Gain",                 UVC_PU_GAIN,                   2, false, null),
            new("Hue",                  UVC_PU_HUE,                    2, true,  "HueAuto"),
            new("Saturation",           UVC_PU_SATURATION,             2, false, null),
            new("Sharpness",            UVC_PU_SHARPNESS,              2, false, null),
            new("Gamma",                UVC_PU_GAMMA,                  2, false, null),
            new("WhiteBalance",         UVC_PU_WHITE_BALANCE_TEMP,     2, false, "AutoWhiteBalance"),
            new("BacklightCompensation",UVC_PU_BACKLIGHT_COMPENSATION, 2, false, null),
            new("PowerLineFrequency",   UVC_PU_POWER_LINE_FREQUENCY,   1, false, null),
        ];

        private static readonly ControlDef[] CtControls =
        [
            new("ExposureTime",     UVC_CT_EXPOSURE_TIME_ABS, 4, false, "AutoExposure"),
            new("FocusAbsolute",    UVC_CT_FOCUS_ABS,         2, false, "AutoFocus"),
            new("Zoom_Absolute",    UVC_CT_ZOOM_ABS,          2, false, null),
            new("Iris",             UVC_CT_IRIS_ABS,          2, false, null),
            new("Roll",             UVC_CT_ROLL_ABS,          2, true,  null),
        ];

        private static readonly AutoControlDef[] AutoControls =
        [
            new("AutoExposure",        UVC_CT_AE_MODE),
            new("AutoFocus",           UVC_CT_FOCUS_AUTO),
            new("AutoWhiteBalance",    UVC_PU_WHITE_BALANCE_TEMP_AUTO),
            new("HueAuto",             UVC_PU_HUE_AUTO),
            new("ContrastAuto",        UVC_PU_CONTRAST_AUTO),
        ];

        public List<ICameraControl> EnumerateControls(Camera camera)
        {
            List<ICameraControl> controls = [];
            bool preferCameraTerminalAliases = _puId == _ctId
                && camera.VendorId == 60324 && camera.ProductId == 4867;

            // Processing unit controls
            foreach (var c in PuControls)
            {
                if (!ControlSupported(_puId, c.Selector)) continue;

                long mn = ReadCtrlGeneric(_puId, c.Selector, c.DataLen, c.IsSigned, LibUvc.UVC_GET_MIN);
                long mx = ReadCtrlGeneric(_puId, c.Selector, c.DataLen, c.IsSigned, LibUvc.UVC_GET_MAX);
                long st = ReadCtrlGeneric(_puId, c.Selector, c.DataLen, c.IsSigned, LibUvc.UVC_GET_RES);
                long df = ReadCtrlGeneric(_puId, c.Selector, c.DataLen, c.IsSigned, LibUvc.UVC_GET_DEF);
                long cv = ReadCtrlGeneric(_puId, c.Selector, c.DataLen, c.IsSigned, LibUvc.UVC_GET_CUR);

                (bool autoSup, bool isAuto) = CheckAuto(c.AutoName);

                if (Enum.TryParse(c.Name, out ControlType ct))
                {
                    var control = new CameraControl(ct, camera);
                    control.ApplyDiscoveredState((int)mn, (int)mx, st, (int)df, (int)cv, autoSup, isAuto, "rw", ControlValueType.Int);
                    controls.Add(control);
                    logger.Info($"UVC control '{c.Name}' min={mn} max={mx} step={st} default={df} value={cv} auto={autoSup} for '{camera.Name}'");
                }

                if (preferCameraTerminalAliases)
                {
                    string aliasName = c.Selector switch
                    {
                        UVC_PU_GAIN => "ExposureTime",
                        UVC_PU_HUE => "FocusAbsolute",
                        _ => c.Name
                    };
                    if (aliasName != c.Name && Enum.TryParse(aliasName, out ControlType aliasType))
                    {
                        var aliasControl = new CameraControl(aliasType, camera);
                        aliasControl.ApplyDiscoveredState((int)mn, (int)mx, st, (int)df, (int)cv, false, false, "rw", ControlValueType.Int);
                        controls.Add(aliasControl);
                        logger.Info($"UVC aliased control '{aliasName}' for '{camera.Name}' (shared selector=0x{c.Selector:X2})");
                    }
                }
            }

            // Camera terminal controls (skip if PU and CT share entity ID)
            if (_ctId == _puId)
            {
                if (!preferCameraTerminalAliases)
                {
                    logger.Warn($"Skipping camera-terminal control enumeration because PU and CT share entity ID {_ctId}.");
                }
                return controls;
            }

            foreach (var c in CtControls)
            {
                if (!ControlSupported(_ctId, c.Selector)) continue;

                long mn = ReadCtrlGeneric(_ctId, c.Selector, c.DataLen, c.IsSigned, LibUvc.UVC_GET_MIN);
                long mx = ReadCtrlGeneric(_ctId, c.Selector, c.DataLen, c.IsSigned, LibUvc.UVC_GET_MAX);
                long st = ReadCtrlGeneric(_ctId, c.Selector, c.DataLen, c.IsSigned, LibUvc.UVC_GET_RES);
                long df = ReadCtrlGeneric(_ctId, c.Selector, c.DataLen, c.IsSigned, LibUvc.UVC_GET_DEF);
                long cv = ReadCtrlGeneric(_ctId, c.Selector, c.DataLen, c.IsSigned, LibUvc.UVC_GET_CUR);

                (bool autoSup, bool isAuto) = CheckAuto(c.AutoName);

                if (Enum.TryParse(c.Name, out ControlType ct))
                {
                    var control = new CameraControl(ct, camera);
                    control.ApplyDiscoveredState((int)mn, (int)mx, st, (int)df, (int)cv, autoSup, isAuto, "rw", ControlValueType.Int);
                    controls.Add(control);
                    logger.Info($"UVC control '{c.Name}' min={mn} max={mx} step={st} default={df} value={cv} auto={autoSup} for '{camera.Name}'");
                }
            }

            return controls;
        }

        /// <summary>
        /// Checks if a UVC control is supported via UVC_GET_INFO.
        /// </summary>
        private bool ControlSupported(byte entityId, byte selector)
        {
            byte[] info = new byte[1];
            int ret = LibUvc.uvc_get_ctrl(_devh, entityId, selector, info, 1, LibUvc.UVC_GET_INFO);
            return ret > 0 && (info[0] & 0x03) != 0;
        }

        /// <summary>
        /// Reads a UVC control value using the generic uvc_get_ctrl API.
        /// </summary>
        private long ReadCtrlGeneric(byte entityId, byte selector, int dataLen, bool isSigned, byte req)
        {
            byte[] buf = new byte[dataLen];
            int ret = LibUvc.uvc_get_ctrl(_devh, entityId, selector, buf, dataLen, req);
            if (ret < 0)
            {
                logger.Debug($"ReadCtrlGeneric failed: entityId={entityId}, selector=0x{selector:X2}, error={LibUvc.ErrorName(ret)}");
                return 0;
            }

            return dataLen switch
            {
                1 => isSigned ? (sbyte)buf[0] : buf[0],
                2 => isSigned ? (short)(buf[0] | (buf[1] << 8)) : (ushort)(buf[0] | (buf[1] << 8)),
                4 => isSigned ? (int)(buf[0] | (buf[1] << 8) | (buf[2] << 16) | (buf[3] << 24))
                              : (uint)(buf[0] | (buf[1] << 8) | (buf[2] << 16) | (buf[3] << 24)),
                _ => 0
            };
        }

        private (bool autoSup, bool isAuto) CheckAuto(string? autoName)
        {
            if (string.IsNullOrEmpty(autoName)) return (false, false);

            var autoDef = AutoControls.FirstOrDefault(a => a.Name == autoName);
            if (autoDef == null) return (false, false);

            byte entityId = autoName is "AutoExposure" or "AutoFocus" ? _ctId : _puId;
            if (!ControlSupported(entityId, autoDef.Selector)) return (false, false);

            byte[] buf = new byte[1];
            int ret = LibUvc.uvc_get_ctrl(_devh, entityId, autoDef.Selector, buf, 1, LibUvc.UVC_GET_CUR);
            return (true, ret > 0 && buf[0] != 0);
        }

        // -------------------------------------------------------------------
        // Control set (called from CameraControlService via MacOSCameraDetect)
        // -------------------------------------------------------------------

        public bool SetControl(string controlName, long value)
        {
            if (_devh == IntPtr.Zero)
            {
                logger.Warn($"Ignoring UVC control set because device handle is closed: control='{controlName}', value={value}");
                return false;
            }

            int ret = controlName switch
            {
                "Brightness"            => LibUvc.uvc_set_brightness(_devh, (short)value),
                "Contrast"              => LibUvc.uvc_set_contrast(_devh, (ushort)value),
                "Gain"                  => LibUvc.uvc_set_gain(_devh, (ushort)value),
                "Hue"                   => LibUvc.uvc_set_hue(_devh, (short)value),
                "Saturation"            => LibUvc.uvc_set_saturation(_devh, (ushort)value),
                "Sharpness"             => LibUvc.uvc_set_sharpness(_devh, (ushort)value),
                "Gamma"                 => LibUvc.uvc_set_gamma(_devh, (ushort)value),
                "WhiteBalance"          => LibUvc.uvc_set_white_balance_temperature(_devh, (ushort)value),
                "BacklightCompensation" => LibUvc.uvc_set_backlight_compensation(_devh, (ushort)value),
                "PowerLineFrequency"    => LibUvc.uvc_set_power_line_frequency(_devh, (byte)value),
                "ExposureTime"          => LibUvc.uvc_set_exposure_abs(_devh, (uint)value),
                "FocusAbsolute"         => LibUvc.uvc_set_focus_abs(_devh, (ushort)value),
                "Zoom_Absolute"         => LibUvc.uvc_set_zoom_abs(_devh, (ushort)value),
                "Iris"                  => LibUvc.uvc_set_iris_abs(_devh, (ushort)value),
                "Roll"                  => LibUvc.uvc_set_roll_abs(_devh, (short)value),
                _ => SetControlGeneric(controlName, value)
            };

            if (ret == LibUvc.UVC_SUCCESS)
            {
                logger.Info($"UVC control '{controlName}' set to {value}: {LibUvc.ErrorName(ret)}");
                return true;
            }
            else
            {
                logger.Warn($"Failed to set UVC control '{controlName}' to {value}: {LibUvc.ErrorName(ret)}");
                return false;
            }
        }

        private int SetControlGeneric(string controlName, long value)
        {
            var (entityId, def) = FindControl(controlName);
            if (def == null)
            {
                logger.Warn($"Unknown UVC control '{controlName}'");
                return LibUvc.UVC_ERROR_NOT_FOUND;
            }

            byte[] buf = new byte[def.DataLen];
            switch (def.DataLen)
            {
                case 1: buf[0] = (byte)value; break;
                case 2: buf[0] = (byte)(value & 0xFF); buf[1] = (byte)((value >> 8) & 0xFF); break;
                case 4: buf[0] = (byte)(value & 0xFF); buf[1] = (byte)((value >> 8) & 0xFF);
                        buf[2] = (byte)((value >> 16) & 0xFF); buf[3] = (byte)((value >> 24) & 0xFF); break;
            }

            return LibUvc.uvc_set_ctrl(_devh, entityId, def.Selector, buf, def.DataLen);
        }

        public bool SetAutoControl(string controlName, bool isAuto)
        {
            if (_devh == IntPtr.Zero)
            {
                logger.Warn($"Ignoring UVC auto-control set because device handle is closed: control='{controlName}', isAuto={isAuto}");
                return false;
            }

            int ret = controlName switch
            {
                "AutoExposure"      => LibUvc.uvc_set_ae_mode(_devh, (byte)(isAuto ? 2 : 1)), // 2=auto, 1=manual
                "AutoFocus"         => LibUvc.uvc_set_focus_auto(_devh, (byte)(isAuto ? 1 : 0)),
                "AutoWhiteBalance"  => LibUvc.uvc_set_white_balance_temperature_auto(_devh, (byte)(isAuto ? 1 : 0)),
                "HueAuto"           => LibUvc.uvc_set_hue_auto(_devh, (byte)(isAuto ? 1 : 0)),
                "ContrastAuto"      => LibUvc.uvc_set_contrast_auto(_devh, (byte)(isAuto ? 1 : 0)),
                _ => SetAutoControlGeneric(controlName, isAuto)
            };

            if (ret == LibUvc.UVC_SUCCESS)
            {
                logger.Info($"UVC auto control '{controlName}' set to {isAuto}: {LibUvc.ErrorName(ret)}");
                return true;
            }
            else
            {
                logger.Warn($"Failed to set UVC auto control '{controlName}' to {isAuto}: {LibUvc.ErrorName(ret)}");
                return false;
            }
        }

        private int SetAutoControlGeneric(string controlName, bool isAuto)
        {
            var autoDef = AutoControls.FirstOrDefault(a => a.Name == controlName);
            if (autoDef == null)
            {
                logger.Warn($"Unknown UVC auto control '{controlName}'");
                return LibUvc.UVC_ERROR_NOT_FOUND;
            }

            byte entityId = controlName is "AutoExposure" or "AutoFocus" ? _ctId : _puId;
            byte[] buf = [(byte)(isAuto ? 1 : 0)];
            return LibUvc.uvc_set_ctrl(_devh, entityId, autoDef.Selector, buf, 1);
        }

        private (byte entityId, ControlDef? def) FindControl(string name)
        {
            bool preferAliases = _puId == _ctId;

            if (preferAliases)
            {
                if (name == "ExposureTime") return (_puId, PuControls.First(c => c.Selector == UVC_PU_GAIN));
                if (name == "FocusAbsolute") return (_puId, PuControls.First(c => c.Selector == UVC_PU_HUE));
            }

            foreach (var c in PuControls)
                if (c.Name == name) return (_puId, c);
            foreach (var c in CtControls)
                if (c.Name == name) return (_ctId, c);
            return (0, null);
        }

        // -------------------------------------------------------------------
        // Streaming
        // -------------------------------------------------------------------

        private bool StartStreaming(Camera camera)
        {
            // Find the best available format by parsing the format descriptors.
            // We prefer MJPEG at the highest resolution ≤ 1920×1080.
            var (fmt, width, height, fps) = FindBestFormat();

            if (fmt == LibUvc.UVC_FRAME_FORMAT_UNKNOWN || width == 0)
            {
                logger.Error("No suitable UVC streaming format found");
                return false;
            }

            logger.Info($"UVC streaming: requesting {width}×{height}@{fps}fps format={fmt} (MJPEG=7)");

            // Negotiate the stream control block via probe/commit
            LibUvc.uvc_stream_ctrl_t ctrl = default;
            int ret = LibUvc.uvc_get_stream_ctrl_format_size(
                _devh, ref ctrl, fmt, width, height, fps);

            if (ret != LibUvc.UVC_SUCCESS)
            {
                logger.Error($"uvc_get_stream_ctrl_format_size failed: {LibUvc.ErrorName(ret)}");
                return false;
            }

            _width = width;
            _height = height;
            logger.Info($"UVC probe: format={ctrl.bFormatIndex}, frame={ctrl.bFrameIndex}, " +
                        $"maxFrameSize={ctrl.dwMaxVideoFrameSize}, maxPayload={ctrl.dwMaxPayloadTransferSize}");

            // Set up the frame callback — store as field to prevent GC collection
            _frameCallback = OnFrameCallback;
            _running = true;
            _loggedNonMjpeg = false;

            ret = LibUvc.uvc_start_streaming(_devh, ref ctrl, _frameCallback, IntPtr.Zero, 0);
            if (ret != LibUvc.UVC_SUCCESS)
            {
                logger.Error($"uvc_start_streaming failed: {LibUvc.ErrorName(ret)}");
                _running = false;
                return false;
            }

            logger.Info($"UVC streaming started ({_width}×{_height})");
            return true;
        }

        /// <summary>
        /// Parses the format/frame descriptors to find the best MJPEG stream.
        /// Falls back to the first available format if MJPEG is not supported.
        /// </summary>
        private (int format, int width, int height, int fps) FindBestFormat()
        {
            int bestFormat = LibUvc.UVC_FRAME_FORMAT_UNKNOWN;
            int bestWidth = 0;
            int bestHeight = 0;
            int bestFps = 30;

            IntPtr formatDescs = LibUvc.uvc_get_format_descs(_devh);
            if (formatDescs == IntPtr.Zero)
            {
                logger.Warn("No format descriptors found");
                return (bestFormat, bestWidth, bestHeight, bestFps);
            }

            // Walk the linked list of format descriptors.
            // uvc_format_desc_t layout (64-bit, with utlist prev/next):
            //   parent:     offset 0   (ptr, 8 bytes)
            //   prev:       offset 8   (ptr, 8 bytes)
            //   next:       offset 16  (ptr, 8 bytes)
            //   bDescriptorSubtype: offset 24  (enum=int, 4 bytes)
            //   bFormatIndex:      offset 28  (byte)
            //   bNumFrameDescriptors: offset 29 (byte)
            //   guidFormat/fourcc:  offset 30  (16 bytes union)
            //   bBitsPerPixel:     offset 46  (byte)
            //   bDefaultFrameIndex: offset 47 (byte)
            //   bAspectRatioX:     offset 48  (byte)
            //   bAspectRatioY:     offset 49  (byte)
            //   bmInterlaceFlags:  offset 50  (byte)
            //   bCopyProtect:      offset 51  (byte)
            //   frame_descs:       offset 56  (ptr, 8 bytes — aligned to 8)
            int offSubtype = IntPtr.Size * 3;
            int offFormatIndex = IntPtr.Size * 3 + 4;
            int offFourcc = IntPtr.Size * 3 + 4 + 1;
            int offFrameDescsPtr = 56; // aligned after 6 bytes of trailing fields

            IntPtr currentFormat = formatDescs;
            int formatIdx = 0;

            while (currentFormat != IntPtr.Zero && formatIdx < 20)
            {
                int subtype = Marshal.ReadInt32(currentFormat, offSubtype);
                byte fmtIdx = Marshal.ReadByte(currentFormat, offFormatIndex);
                byte[] fourcc = new byte[4];
                Marshal.Copy(currentFormat + offFourcc, fourcc, 0, 4);
                string fourccStr = System.Text.Encoding.ASCII.GetString(fourcc);

                bool isMjpeg = subtype == LibUvc.UVC_VS_FORMAT_MJPEG || fourccStr == "MJPG";
                logger.Debug($"FindBestFormat: format[{formatIdx}] subtype=0x{subtype:X2} idx={fmtIdx} fourcc='{fourccStr}' mjep={isMjpeg}");

                IntPtr frameDescsPtr = Marshal.ReadIntPtr(currentFormat, offFrameDescsPtr);
                if (frameDescsPtr != IntPtr.Zero)
                {
                    // uvc_frame_desc_t layout (64-bit, with utlist prev/next):
                    //   parent:     offset 0   (ptr)
                    //   prev:       offset 8   (ptr)
                    //   next:       offset 16  (ptr)
                    //   bDescriptorSubtype: offset 24 (enum=int, 4 bytes)
                    //   bFrameIndex:      offset 28  (byte)
                    //   bmCapabilities:   offset 29  (byte)
                    //   wWidth:           offset 30  (ushort)
                    //   wHeight:          offset 32  (ushort)
                    int fdOffFrameIdx = IntPtr.Size * 3 + 4;
                    int fdOffWidth = IntPtr.Size * 3 + 4 + 1 + 1;
                    int fdOffHeight = fdOffWidth + 2;

                    IntPtr currentFrame = frameDescsPtr;
                    int frameIdx = 0;

                    while (currentFrame != IntPtr.Zero && frameIdx < 30)
                    {
                        byte frameIndex = Marshal.ReadByte(currentFrame, fdOffFrameIdx);
                        ushort w = (ushort)(Marshal.ReadByte(currentFrame, fdOffWidth) | (Marshal.ReadByte(currentFrame, fdOffWidth + 1) << 8));
                        ushort h = (ushort)(Marshal.ReadByte(currentFrame, fdOffHeight) | (Marshal.ReadByte(currentFrame, fdOffHeight + 1) << 8));

                        logger.Debug($"FindBestFormat:   frame[{frameIdx}] idx={frameIndex} {w}×{h}");

                        // Prefer MJPEG at highest resolution ≤ 1920×1080
                        if (isMjpeg && w > 0 && h > 0 && w <= 1920 && h <= 1080)
                        {
                            if (bestFormat != LibUvc.UVC_FRAME_FORMAT_MJPEG || (w * h > bestWidth * bestHeight))
                            {
                                bestFormat = LibUvc.UVC_FRAME_FORMAT_MJPEG;
                                bestWidth = w;
                                bestHeight = h;
                            }
                        }
                        // Fallback: accept any format at a reasonable resolution
                        else if (bestFormat == LibUvc.UVC_FRAME_FORMAT_UNKNOWN && w > 0 && h > 0 && w <= 1920 && h <= 1080)
                        {
                            bestFormat = LibUvc.UVC_FRAME_FORMAT_YUYV;
                            bestWidth = w;
                            bestHeight = h;
                        }

                        currentFrame = Marshal.ReadIntPtr(currentFrame, IntPtr.Size * 2); // next
                        frameIdx++;
                    }
                }

                currentFormat = Marshal.ReadIntPtr(currentFormat, IntPtr.Size * 2); // next
                formatIdx++;
            }

            if (bestFormat == LibUvc.UVC_FRAME_FORMAT_UNKNOWN)
            {
                logger.Warn("FindBestFormat: no format found via descriptor walk, trying MJPEG 640×480 fallback");
                bestFormat = LibUvc.UVC_FRAME_FORMAT_MJPEG;
                bestWidth = 640;
                bestHeight = 480;
            }

            return (bestFormat, bestWidth, bestHeight, bestFps);
        }

        // -------------------------------------------------------------------
        // Frame callback (called by libuvc on the streaming thread)
        // -------------------------------------------------------------------

        private void OnFrameCallback(IntPtr framePtr, IntPtr userPtr)
        {
            if (!_running || framePtr == IntPtr.Zero) return;

            try
            {
                var frame = Marshal.PtrToStructure<LibUvc.uvc_frame_t>(framePtr);
                if (frame.data == IntPtr.Zero || (long)frame.data_bytes == 0) return;

                // Update dimensions from the frame
                if (_width == 0 || _height == 0)
                {
                    _width = (int)frame.width;
                    _height = (int)frame.height;
                    logger.Info($"UVC frame dimensions from callback: {frame.width}×{frame.height}");
                }

                int dataLen = (int)(long)frame.data_bytes;
                byte[] frameData = new byte[dataLen];
                Marshal.Copy(frame.data, frameData, 0, dataLen);

                // For MJPEG, frameData is a complete JPEG — deliver to FrameRenderer.
                if (frame.frame_format == LibUvc.UVC_FRAME_FORMAT_MJPEG)
                {
                    FrameReady?.Invoke(frameData, _width, _height);
                }
                else
                {
                    // Uncompressed format (YUYV, etc.) — log once.
                    // Could add YUYV→JPEG conversion later if needed.
                    if (!_loggedNonMjpeg)
                    {
                        _loggedNonMjpeg = true;
                        logger.Warn($"UVC frame format {frame.frame_format} is not MJPEG — frames are being dropped. " +
                                    "Add YUYV→JPEG conversion or request MJPEG format from the camera.");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in UVC frame callback");
            }
        }

        // -------------------------------------------------------------------
        // Recovery (called from Program.cs --recover-uvc-vidpid)
        // -------------------------------------------------------------------

        /// <summary>
        /// Restores the macOS kernel UVC driver by re-activating the device
        /// configuration via IOKit SetConfiguration(1). Useful after a crash
        /// that left the device detached from the kernel driver.
        /// </summary>
        internal static bool TryRecoverUvcDevice(int vendorId, int productId)
        {
            if (vendorId <= 0 || productId <= 0) return false;

            try
            {
                logger.Info($"TryRecoverUvcDevice: VID={vendorId} PID={productId}");
                IokitHelper.RestoreKernelDriver(vendorId, productId);
                logger.Info("TryRecoverUvcDevice: recovery succeeded");
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "TryRecoverUvcDevice: recovery threw exception");
                return false;
            }
        }

        // -------------------------------------------------------------------
        // IDisposable
        // -------------------------------------------------------------------

        public void Dispose()
        {
            Stop();
        }
    }
}