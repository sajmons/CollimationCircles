#!/bin/bash
#
# Build/extract libusb for macOS.
# libusb is available from Homebrew — this script copies it from the
# Homebrew installation, fixes the install name, and code-signs it.
#
# Prerequisites:
#   brew install libusb         (arm64, on Apple Silicon)
#   # For x64 on arm64 Macs:
#   arch -x86_64 /usr/local/bin/brew install libusb
#
set -e

PLATFORM="${1:?Usage: build-libusb.sh <arm64|x64>}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

if [ "$PLATFORM" = "arm64" ]; then
  SRC=$(pkg-config --variable=libdir libusb-1.0 2>/dev/null || echo "/opt/homebrew/opt/libusb/lib")
  OUTPUT_DIR="$SCRIPT_DIR/macos/arm64"
elif [ "$PLATFORM" = "x64" ]; then
  SRC="/usr/local/lib"
  OUTPUT_DIR="$SCRIPT_DIR/macos/x64"
else
  echo "ERROR: Unknown arch: $PLATFORM (use arm64 or x64)"
  exit 1
fi

DYLIB="${SRC}/libusb-1.0.0.dylib"

if [ ! -f "$DYLIB" ]; then
  echo "ERROR: libusb-1.0.0.dylib not found at $SRC"
  if [ "$PLATFORM" = "x64" ]; then
    echo "Install Intel Homebrew + libusb:"
    echo "  arch -x86_64 /usr/local/bin/brew install libusb"
  else
    echo "Install with: brew install libusb"
  fi
  exit 1
fi

echo "=== Building libusb for macOS/${PLATFORM} ==="
mkdir -p "$OUTPUT_DIR"
cp "$DYLIB" "$OUTPUT_DIR/libusb-1.0.0.dylib"

# Fix install name
install_name_tool -id @rpath/libusb-1.0.0.dylib "$OUTPUT_DIR/libusb-1.0.0.dylib"

# Code-sign
codesign --force --sign - "$OUTPUT_DIR/libusb-1.0.0.dylib"

echo "=== Done ==="
file "$OUTPUT_DIR/libusb-1.0.0.dylib"
otool -L "$OUTPUT_DIR/libusb-1.0.0.dylib"