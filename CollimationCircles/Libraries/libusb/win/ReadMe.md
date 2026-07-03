# libusb for Windows

This directory contains pre-built libusb-1.0.dll binaries for Windows.

## Building libusb from source

1. Install vcpkg (https://github.com/microsoft/vcpkg)
2. Install libusb:
   ```
   vcpkg install libusb:x64-windows
   vcpkg install libusb:arm64-windows
   ```
3. Copy the built DLLs from `vcpkg/installed/<triplet>/bin/libusb-1.0.dll` into the
   corresponding `win/x64/` or `win/arm64/` directory.

## Current binaries

The included `libusb-1.0.dll` files were built with vcpkg and are redistributed
under the LGPL-2.1 license. See `COPYING.LESSER` in the repository root.