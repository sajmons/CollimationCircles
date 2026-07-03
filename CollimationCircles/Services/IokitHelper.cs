using System;
using System.Runtime.InteropServices;

namespace CollimationCircles.Services
{
    /// <summary>
    /// Minimal IOKit P/Invoke helper for detaching the macOS kernel UVC driver.
    ///
    /// On macOS, the kernel UVC driver (AppleUSBHostUVCDriver / AppleUserUVC)
    /// claims the camera's USB interfaces, which prevents libusb (and therefore
    /// libuvc) from accessing them. IOKit's SetConfiguration(0) detaches the
    /// kernel driver WITHOUT root privileges. After reconfiguring, libuvc races
    /// to claim the interface before the kernel driver re-attaches.
    ///
    /// This is the only piece that libuvc cannot do on its own on macOS —
    /// libuvc's libusb_detach_kernel_driver is not sufficient because the
    /// kernel driver is not a "module" that libusb can detach; it's an IOKit
    /// service that needs to be unconfigured at the device level.
    /// </summary>
    internal static class IokitHelper
    {
        private const string IOKitFramework = "/System/Library/Frameworks/IOKit.framework/IOKit";
        private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        private const uint kCFStringEncodingUTF8 = 0x08000100;

        // IOKit UUIDs for USB device interface (from IOUSBLib.h and IOCFPlugIn.h)
        public static readonly Guid kIOUSBDeviceUserClientTypeID = new("9dc7b780-9ec0-11d4-a54f-000a27052861");
        public static readonly Guid kIOCFPlugInInterfaceID = new("c244e858-109c-11d4-91d4-0050e4c6426f");
        public static readonly Guid kIOUSBDeviceInterfaceID = new("5c8187d0-9ef3-11d4-8b45-000a27052861");

        // IOKit USB constants
        private const string kIOUSBDeviceClassName = "IOUSBDevice";
        private const string kUSBVendorID = "idVendor";
        private const string kUSBProductID = "idProduct";

        // -------------------------------------------------------------------
        // P/Invoke declarations
        // -------------------------------------------------------------------

        [DllImport(IOKitFramework)]
        private static extern IntPtr IOServiceMatching(string name);

        [DllImport(IOKitFramework)]
        private static extern int IOServiceGetMatchingServices(IntPtr mainPort, IntPtr matchingDict, out int iter);

        [DllImport(IOKitFramework)]
        private static extern int IOIteratorNext(int iter);

        [DllImport(IOKitFramework)]
        private static extern int IOObjectRelease(int obj);

        [DllImport(IOKitFramework)]
        private static extern int IOCreatePlugInInterfaceForService(
            int service, IntPtr typeID, IntPtr interfaceID,
            out IntPtr plugInInterface, out int score);

        [DllImport(IOKitFramework)]
        private static extern int IODestroyPlugInInterface(IntPtr interfaceRef);

        [DllImport(CoreFoundation)]
        private static extern IntPtr CFNumberCreate(IntPtr alloc, int theType, ref int value);

        [DllImport(CoreFoundation)]
        private static extern void CFDictionarySetValue(IntPtr dict, IntPtr key, IntPtr value);

        [DllImport(CoreFoundation)]
        private static extern void CFRelease(IntPtr cf);

        [DllImport(CoreFoundation)]
        private static extern IntPtr CFUUIDCreateFromUUIDBytes(IntPtr alloc, CFUUIDBytes uuidBytes);

        // -------------------------------------------------------------------
        // CFUUID / Guid byte-order conversion
        // -------------------------------------------------------------------

        [StructLayout(LayoutKind.Sequential)]
        public struct CFUUIDBytes
        {
            public byte byte0; public byte byte1; public byte byte2; public byte byte3;
            public byte byte4; public byte byte5; public byte byte6; public byte byte7;
            public byte byte8; public byte byte9; public byte byte10; public byte byte11;
            public byte byte12; public byte byte13; public byte byte14; public byte byte15;
        }

        private static CFUUIDBytes CreateCFUUIDBytes(Guid guid)
        {
            byte[] g = guid.ToByteArray();
            byte[] cf = new byte[16];
            cf[0] = g[3]; cf[1] = g[2]; cf[2] = g[1]; cf[3] = g[0];
            cf[4] = g[5]; cf[5] = g[4];
            cf[6] = g[7]; cf[7] = g[6];
            Array.Copy(g, 8, cf, 8, 8);
            return new CFUUIDBytes
            {
                byte0 = cf[0], byte1 = cf[1], byte2 = cf[2], byte3 = cf[3],
                byte4 = cf[4], byte5 = cf[5], byte6 = cf[6], byte7 = cf[7],
                byte8 = cf[8], byte9 = cf[9], byte10 = cf[10], byte11 = cf[11],
                byte12 = cf[12], byte13 = cf[13], byte14 = cf[14], byte15 = cf[15],
            };
        }

        private static IntPtr CreateCFUUIDRef(Guid guid)
        {
            return CFUUIDCreateFromUUIDBytes(IntPtr.Zero, CreateCFUUIDBytes(guid));
        }

        // -------------------------------------------------------------------
        // CFString helper
        // -------------------------------------------------------------------

        [DllImport(CoreFoundation, EntryPoint = "CFStringCreateWithCString")]
        private static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string cStr, uint encoding);

        private static IntPtr CreateCFString(string s) => CFStringCreateWithCString(IntPtr.Zero, s, kCFStringEncodingUTF8);

        // -------------------------------------------------------------------
        // IOKit C COM interface delegates
        // -------------------------------------------------------------------

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int QueryInterfaceDelegate(IntPtr self, CFUUIDBytes iid, out IntPtr result);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate uint ReleaseDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int UsbDeviceOpenDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int UsbDeviceCloseDelegate(IntPtr self);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int SetConfigurationDelegate(IntPtr self, byte configuration);

        // -------------------------------------------------------------------
        // Core: SetConfiguration
        // -------------------------------------------------------------------

        /// <summary>
        /// Uses IOKit to open the USB device and set its configuration.
        /// SetConfiguration(0) detaches the kernel UVC driver (no root needed).
        /// SetConfiguration(1) re-attaches it (restores normal camera operation).
        /// Returns the configuration value that was active before, or 0 on failure.
        /// </summary>
        public static int SetConfiguration(int vendorId, int productId, int targetConfig)
        {
            try
            {
                IntPtr matchingDict = IOServiceMatching(kIOUSBDeviceClassName);
                if (matchingDict == IntPtr.Zero) return 0;

                int vid = vendorId, pid = productId;
                IntPtr vidRef = CFNumberCreate(IntPtr.Zero, 3, ref vid);
                IntPtr pidRef = CFNumberCreate(IntPtr.Zero, 3, ref pid);
                IntPtr vendorKey = CreateCFString(kUSBVendorID);
                IntPtr productKey = CreateCFString(kUSBProductID);

                CFDictionarySetValue(matchingDict, vendorKey, vidRef);
                CFDictionarySetValue(matchingDict, productKey, pidRef);

                CFRelease(vidRef);
                CFRelease(pidRef);
                if (vendorKey != IntPtr.Zero) CFRelease(vendorKey);
                if (productKey != IntPtr.Zero) CFRelease(productKey);

                int kr = IOServiceGetMatchingServices(IntPtr.Zero, matchingDict, out int iter);
                if (kr != 0) return 0;

                int usbDevice = IOIteratorNext(iter);
                IOObjectRelease(iter);
                if (usbDevice == 0) return 0;

                IntPtr typeIDRef = CreateCFUUIDRef(kIOUSBDeviceUserClientTypeID);
                IntPtr ifaceIDRef = CreateCFUUIDRef(kIOCFPlugInInterfaceID);

                kr = IOCreatePlugInInterfaceForService(usbDevice, typeIDRef, ifaceIDRef,
                    out IntPtr plugInInterface, out int score);

                if (typeIDRef != IntPtr.Zero) CFRelease(typeIDRef);
                if (ifaceIDRef != IntPtr.Zero) CFRelease(ifaceIDRef);

                if (kr != 0 || plugInInterface == IntPtr.Zero)
                {
                    IOObjectRelease(usbDevice);
                    return 0;
                }

                IntPtr devInterface = QueryInterfaceForUsbDevice(plugInInterface);
                IODestroyPlugInInterface(plugInInterface);
                IOObjectRelease(usbDevice);

                if (devInterface == IntPtr.Zero) return 0;

                int result = UsbDeviceOpenSetConfig(devInterface, targetConfig);
                ReleaseUsbDeviceInterface(devInterface);
                return result;
            }
            catch
            {
                return 0;
            }
        }

        private static IntPtr QueryInterfaceForUsbDevice(IntPtr plugInInterface)
        {
            IntPtr ifacePtr = Marshal.ReadIntPtr(plugInInterface);
            if (ifacePtr == IntPtr.Zero) return IntPtr.Zero;

            IntPtr queryInterfacePtr = Marshal.ReadIntPtr(ifacePtr, IntPtr.Size * 1);
            if (queryInterfacePtr == IntPtr.Zero) return IntPtr.Zero;

            CFUUIDBytes iid = CreateCFUUIDBytes(kIOUSBDeviceInterfaceID);
            var queryInterface = Marshal.GetDelegateForFunctionPointer<QueryInterfaceDelegate>(queryInterfacePtr);
            int hr = queryInterface(plugInInterface, iid, out IntPtr result);
            return hr != 0 ? IntPtr.Zero : result;
        }

        private static void ReleaseUsbDeviceInterface(IntPtr devInterface)
        {
            if (devInterface == IntPtr.Zero) return;
            IntPtr ifacePtr = Marshal.ReadIntPtr(devInterface);
            if (ifacePtr == IntPtr.Zero) return;
            IntPtr releasePtr = Marshal.ReadIntPtr(ifacePtr, IntPtr.Size * 3);
            if (releasePtr == IntPtr.Zero) return;
            var release = Marshal.GetDelegateForFunctionPointer<ReleaseDelegate>(releasePtr);
            _ = release(devInterface);
        }

        private static int UsbDeviceOpenSetConfig(IntPtr devInterface, int targetConfig)
        {
            IntPtr ifacePtr = Marshal.ReadIntPtr(devInterface);
            if (ifacePtr == IntPtr.Zero) return 0;

            // USBDeviceOpen (index 8)
            IntPtr openPtr = Marshal.ReadIntPtr(ifacePtr, IntPtr.Size * 8);
            var open = Marshal.GetDelegateForFunctionPointer<UsbDeviceOpenDelegate>(openPtr);
            int kr = open(devInterface);
            if (kr != 0) return 0;

            int result = 0;

            // SetConfiguration (index 23)
            IntPtr setConfigPtr = Marshal.ReadIntPtr(ifacePtr, IntPtr.Size * 23);
            var setConfig = Marshal.GetDelegateForFunctionPointer<SetConfigurationDelegate>(setConfigPtr);
            kr = setConfig(devInterface, (byte)targetConfig);

            // USBDeviceClose (index 9)
            IntPtr closePtr = Marshal.ReadIntPtr(ifacePtr, IntPtr.Size * 9);
            var close = Marshal.GetDelegateForFunctionPointer<UsbDeviceCloseDelegate>(closePtr);
            close(devInterface);

            // SetConfiguration(0) detaches the driver; return 1 as "active config was 1"
            // SetConfiguration(N) re-attaches; return N
            result = targetConfig == 0 ? 1 : targetConfig;
            return result;
        }

        /// <summary>
        /// Restores the kernel driver by setting configuration back to 1.
        /// Call this after uvc_close to give the camera back to macOS.
        /// </summary>
        public static void RestoreKernelDriver(int vendorId, int productId)
        {
            if (vendorId <= 0 || productId <= 0) return;
            try
            {
                SetConfiguration(vendorId, productId, 1);
            }
            catch { }
        }
    }
}