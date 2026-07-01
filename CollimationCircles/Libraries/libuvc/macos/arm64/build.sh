#!/bin/bash
# Build libuvc.dylib for macOS/arm64
# Prerequisites: brew install libusb cmake
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
exec "$SCRIPT_DIR/../../build-libuvc-common.sh" macos arm64 "$SCRIPT_DIR"