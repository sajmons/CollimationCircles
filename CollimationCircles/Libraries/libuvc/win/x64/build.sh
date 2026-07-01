#!/bin/bash
# Build libuvc.dll for Windows/x64
# Prerequisites: vcpkg install libusb, cmake, Visual Studio
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
exec "$SCRIPT_DIR/../../build-libuvc-common.sh" win x64 "$SCRIPT_DIR"