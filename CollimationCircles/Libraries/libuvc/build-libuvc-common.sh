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

# Apply patches (Python-based, reliable across platforms)
if [ "$APPLY_PATCHES" != "--no-patches" ]; then
  python3 -c "
import sys

with open('src/device.c', 'r') as f:
    content = f.read()

# Patch 1 (macOS only): add libusb_set_configuration in uvc_open
if '$PLATFORM' == 'macos':
    old1 = '''  ret = uvc_open_internal(dev, usb_devh, devh);
  UVC_EXIT(ret);
  return ret;
}

static uvc_error_t uvc_open_internal('''
    new1 = '''  /* macOS: after IOKit SetConfiguration(0) detached the kernel driver,
   * the device is unconfigured (config 0). Re-activate the configuration
   * through libusb so interfaces become claimable. */
  #ifdef __APPLE__
  {
    int cfg_ret = libusb_set_configuration(usb_devh, 1);
    UVC_DEBUG(\"libusb_set_configuration(1) = %d\", cfg_ret);
    if (cfg_ret != LIBUSB_SUCCESS && cfg_ret != LIBUSB_ERROR_BUSY) {
      UVC_DEBUG(\"libusb_set_configuration failed: %d\", cfg_ret);
    }
  }
  #endif

  ret = uvc_open_internal(dev, usb_devh, devh);
  UVC_EXIT(ret);
  return ret;
}

static uvc_error_t uvc_open_internal('''
    if old1 not in content:
        print('ERROR: Could not find uvc_open anchor text for patch 1', file=sys.stderr)
        sys.exit(1)
    content = content.replace(old1, new1, 1)
    print('Patch 1 applied: uvc_open libusb_set_configuration')

# Patch 2 (macOS only): try claim_interface even when detach fails with ERROR_ACCESS
if '$PLATFORM' == 'macos':
    old2 = '  if (ret == UVC_SUCCESS || ret == LIBUSB_ERROR_NOT_FOUND || ret == LIBUSB_ERROR_NOT_SUPPORTED) {'
    new2 = '  if (ret == UVC_SUCCESS || ret == LIBUSB_ERROR_NOT_FOUND || ret == LIBUSB_ERROR_NOT_SUPPORTED\n      || ret == LIBUSB_ERROR_ACCESS) {'
    if old2 not in content:
        print('ERROR: Could not find uvc_claim_if anchor text for patch 2', file=sys.stderr)
        sys.exit(1)
    content = content.replace(old2, new2, 1)
    print('Patch 2 applied: uvc_claim_if ERROR_ACCESS handling')

with open('src/device.c', 'w') as f:
    f.write(content)
"
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
    EXTRA_CMAKE="-DLIBUSB_INCLUDE_DIR=/usr/local/include/libusb-1.0 -DLIBUSB_LIBRARY=/usr/local/lib/libusb-1.0.0.dylib"
  else
    EXTRA_CMAKE=""
  fi

  cmake .. \
    -DBUILD_SHARED_LIBS=ON \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_POLICY_VERSION_MINIMUM=3.5 \
    -DCMAKE_DISABLE_FIND_PACKAGE_JpegPkg=ON \
    -DCMAKE_OSX_ARCHITECTURES="${ARCH}" \
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