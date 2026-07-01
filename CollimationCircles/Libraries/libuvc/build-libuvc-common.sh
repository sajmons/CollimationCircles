#!/bin/bash
#
# Build libuvc for a specific platform/arch.
# This is the common build logic used by all platform-specific scripts.
#
# Usage: build-libuvc-common.sh <platform> <arch> <output-dir> [--no-patches]
#
# Patches in patches/ are applied automatically (skipped with --no-patches).
# Patches 0001 and 0002 are macOS-only — they are skipped on other platforms.
#
set -e

PLATFORM="${1:?Usage: build-libuvc-common.sh <platform> <arch> <output-dir>}"
ARCH="${2:?Missing arch}"
OUTPUT_DIR="${3:?Missing output-dir}"
APPLY_PATCHES="${4:---patches}"

BUILD_DIR="$(mktemp -d /tmp/libuvc-build-XXXXXX)"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "=== Building libuvc: ${PLATFORM}/${ARCH} ==="
echo "Build dir: $BUILD_DIR"

# Clone libuvc
git clone --depth 1 https://github.com/libuvc/libuvc.git "$BUILD_DIR/libuvc"
cd "$BUILD_DIR/libuvc"

# Apply patches
if [ "$APPLY_PATCHES" != "--no-patches" ]; then
  for patch_file in "$SCRIPT_DIR"/patches/*.patch; do
    patch_name="$(basename "$patch_file")"
    # macOS patches only apply on macOS
    if echo "$patch_name" | grep -q "macos" && [ "$PLATFORM" != "macos" ]; then
      echo "Skipping macOS-only patch: $patch_name"
      continue
    fi
    echo "Applying patch: $patch_name"
    git apply "$patch_file" || { echo "ERROR: Failed to apply $patch_name"; exit 1; }
  done
fi

# Build
mkdir -p build && cd build

if [ "$PLATFORM" = "macos" ]; then
  # macOS: build as dylib, fix install names, code-sign
  LIBUSB_PATH=$(pkg-config --variable=libdir libusb-1.0 2>/dev/null || echo "/opt/homebrew/opt/libusb/lib")
  # On x64, prefer Intel Homebrew
  if [ "$ARCH" = "x64" ] && [ -d "/usr/local/lib" ]; then
    LIBUSB_PATH="/usr/local/lib"
    export PKG_CONFIG_PATH="/usr/local/lib/pkgconfig:${PKG_CONFIG_PATH}"
  fi

  cmake .. \
    -DBUILD_SHARED_LIBS=ON \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_POLICY_VERSION_MINIMUM=3.5 \
    -DCMAKE_DISABLE_FIND_PACKAGE_JpegPkg=ON \
    -DCMAKE_OSX_ARCHITECTURES="${ARCH}" \
    ${ARCH:+$([ "$ARCH" = "x64" ] && echo "-DLIBUSB_INCLUDE_DIR=/usr/local/include/libusb-1.0 -DLIBUSB_LIBRARY=/usr/local/lib/libusb-1.0.0.dylib")}
  make -j$(sysctl -n hw.ncpu 2>/dev/null || echo 4)

  # Fix install names
  install_name_tool -id @rpath/libuvc.dylib libuvc.dylib
  # Try both the Cellar path and the opt symlink path
  install_name_tool -change "${LIBUSB_PATH}/libusb-1.0.0.dylib" @loader_path/libusb-1.0.0.dylib libuvc.dylib 2>/dev/null || true
  install_name_tool -change "/opt/homebrew/opt/libusb/lib/libusb-1.0.0.dylib" @loader_path/libusb-1.0.0.dylib libuvc.dylib 2>/dev/null || true

  OUTPUT_FILE="libuvc.dylib"

elif [ "$PLATFORM" = "linux" ]; then
  # Linux: build as .so
  cmake .. \
    -DBUILD_SHARED_LIBS=ON \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_POLICY_VERSION_MINIMUM=3.5 \
    -DCMAKE_DISABLE_FIND_PACKAGE_JpegPkg=ON
  make -j$(nproc 2>/dev/null || echo 4)

  OUTPUT_FILE="libuvc.so"

elif [ "$PLATFORM" = "win" ]; then
  # Windows: build as .dll (requires MinGW or MSVC + vcpkg)
  # libusb must be available via vcpkg or manually installed
  cmake .. \
    -DBUILD_SHARED_LIBS=ON \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_POLICY_VERSION_MINIMUM=3.5 \
    -DCMAKE_DISABLE_FIND_PACKAGE_JpegPkg=ON \
    -DCMAKE_GENERATOR_PLATFORM="${ARCH}"
  cmake --build . --config Release -j$(nproc 2>/dev/null || echo 4)

  OUTPUT_FILE="Release/libuvc.dll"

else
  echo "ERROR: Unknown platform: $PLATFORM"
  exit 1
fi

# Deploy
mkdir -p "$OUTPUT_DIR"
cp "$OUTPUT_FILE" "$OUTPUT_DIR/"

# Code-sign on macOS
if [ "$PLATFORM" = "macos" ]; then
  codesign --force --sign - "$OUTPUT_DIR/$(basename $OUTPUT_FILE)"
fi

echo ""
echo "=== Done: ${PLATFORM}/${ARCH} ==="
file "$OUTPUT_DIR/$(basename $OUTPUT_FILE)"

# Cleanup
rm -rf "$BUILD_DIR"