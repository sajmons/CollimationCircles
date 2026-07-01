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

# Normalize macOS arch: x64 → x86_64 for clang/cmake
CMAKE_ARCH="$ARCH"
if [ "$PLATFORM" = "macos" ] && [ "$ARCH" = "x64" ]; then
  CMAKE_ARCH="x86_64"
fi

echo "=== Building libuvc: ${PLATFORM}/${ARCH} ==="
echo "Build dir: $BUILD_DIR"

# Clone libuvc
git clone --depth 1 https://github.com/libuvc/libuvc.git "$BUILD_DIR/libuvc"
cd "$BUILD_DIR/libuvc"

# Apply patches from patches/ directory
if [ "$APPLY_PATCHES" != "--no-patches" ]; then
  PATCH_DIR="$SCRIPT_DIR/patches"
  if [ -d "$PATCH_DIR" ]; then
    for patch_file in $(find "$PATCH_DIR" -name '*.patch' | sort); do
      patch_name="$(basename "$patch_file")"
      # Skip patches explicitly tagged macos-only on other platforms
      if echo "$patch_name" | grep -q "macos-only" && [ "$PLATFORM" != "macos" ]; then
        echo "Skipping macOS-only patch: $patch_name"
        continue
      fi
      echo "Applying patch: $patch_name"
      git apply "$patch_file" || { echo "ERROR: Failed to apply $patch_name"; exit 1; }
    done
  else
    echo "No patches directory found at: $PATCH_DIR"
  fi
fi

# Build
mkdir -p build && cd build

if [ "$PLATFORM" = "macos" ]; then
  # macOS: build as dylib, fix install names, code-sign
  LIBUSB_PATH=$(pkg-config --variable=libdir libusb-1.0 2>/dev/null || echo "/opt/homebrew/opt/libusb/lib")
  EXTRA_CMAKE=""
  # On x64, prefer Intel Homebrew
  if [ "$ARCH" = "x64" ] && [ -d "/usr/local/lib" ]; then
    LIBUSB_PATH="/usr/local/lib"
    export PKG_CONFIG_PATH="/usr/local/lib/pkgconfig:${PKG_CONFIG_PATH}"
    EXTRA_CMAKE="-DLIBUSB_INCLUDE_DIR=/usr/local/include/libusb-1.0 -DLIBUSB_LIBRARY=/usr/local/lib/libusb-1.0.0.dylib"
  fi

  cmake .. \
    -DBUILD_SHARED_LIBS=ON \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_POLICY_VERSION_MINIMUM=3.5 \
    -DCMAKE_DISABLE_FIND_PACKAGE_JpegPkg=ON \
    -DCMAKE_OSX_ARCHITECTURES="${CMAKE_ARCH}" \
    $EXTRA_CMAKE
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
  # Resolve vcpkg toolchain
  if [ -z "$VCPKG_ROOT" ]; then
    echo "ERROR: VCPKG_ROOT is not set"; exit 1
  fi
  TOOLCHAIN="$(cygpath -m "$VCPKG_ROOT")/scripts/buildsystems/vcpkg.cmake"

  cmake .. \
    -DBUILD_SHARED_LIBS=ON \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_POLICY_VERSION_MINIMUM=3.5 \
    -DCMAKE_DISABLE_FIND_PACKAGE_JpegPkg=ON \
    -DCMAKE_TOOLCHAIN_FILE="$TOOLCHAIN" \
    -DVCPKG_TARGET_TRIPLET="${VCPKG_DEFAULT_TRIPLET:-x64-windows}" \
    -A "${ARCH}"
  cmake --build . --config Release

  OUTPUT_FILE="Release/libuvc.dll"

else
  echo "ERROR: Unknown platform: $PLATFORM"; exit 1
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