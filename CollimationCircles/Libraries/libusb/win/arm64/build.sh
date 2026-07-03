#!/bin/bash
# Build/download libusb-1.0.dll for Windows/arm64
# Prerequisites: vcpkg install libusb:arm64-windows
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

VCPKG_ROOT="${VCPKG_ROOT:-C:/vcpkg}"
if [ -d "$VCPKG_ROOT" ]; then
    "$VCPKG_ROOT/vcpkg" install libusb:arm64-windows
    cp "$VCPKG_ROOT/installed/arm64-windows/bin/libusb-1.0.dll" "$SCRIPT_DIR/"
    echo "Copied libusb-1.0.dll to $SCRIPT_DIR/"
else
    echo "vcpkg not found at $VCPKG_ROOT"
    echo "Download libusb-1.0.dll from https://github.com/libusb/libusb/releases and place it in $SCRIPT_DIR/"
    exit 1
fi