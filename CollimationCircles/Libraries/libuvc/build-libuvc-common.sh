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
echo "Output dir: $OUTPUT_DIR"
echo "Patches: ${APPLY_PATCHES}"

# Clone libuvc
echo "[Step] Cloning libuvc repository..."
git clone --depth 1 https://github.com/libuvc/libuvc.git "$BUILD_DIR/libuvc"
cd "$BUILD_DIR/libuvc"
echo "[Done] Cloned libuvc at commit: $(git rev-parse --short HEAD)"

# Apply patches from patches/ directory

# Apply patches from patches/ directory
# Patch filenames use a prefix to indicate which platforms they apply to:
#   0001-macos-only-*  → applied only on macOS
#   0004-win-only-*    → applied only on Windows
#   0005-all-*         → applied on all platforms
if [ "$APPLY_PATCHES" != "--no-patches" ]; then
  PATCH_DIR="$SCRIPT_DIR/patches"
  if [ -d "$PATCH_DIR" ]; then
    for patch_file in $(find "$PATCH_DIR" -maxdepth 1 -name '*.patch' | sort); do
      patch_name="$(basename "$patch_file")"
      # Determine target platform from filename
      if echo "$patch_name" | grep -q "macos-only" && [ "$PLATFORM" != "macos" ]; then
        echo "Skipping macOS-only patch: $patch_name"
        continue
      fi
      if echo "$patch_name" | grep -q "win-only" && [ "$PLATFORM" != "win" ]; then
        echo "Skipping Windows-only patch: $patch_name"
        continue
      fi
      echo "Applying patch: $patch_name"
      git apply "$patch_file" || { echo "ERROR: Failed to apply $patch_name"; exit 1; }
    done
  else
    echo "WARNING: No patches directory found at: $PATCH_DIR"
  fi
fi

# Build
mkdir -p build && cd build

if [ "$PLATFORM" = "macos" ]; then
  # macOS: build as dylib, fix install names, code-sign
  echo "[Step] macOS build — detecting libusb path..."
  LIBUSB_PATH=$(pkg-config --variable=libdir libusb-1.0 2>/dev/null || echo "/opt/homebrew/opt/libusb/lib")
  echo "[Info] LIBUSB_PATH=$LIBUSB_PATH"
  EXTRA_CMAKE=""
  # On x64, prefer Intel Homebrew
  if [ "$ARCH" = "x64" ] && [ -d "/usr/local/lib" ]; then
    LIBUSB_PATH="/usr/local/lib"
    export PKG_CONFIG_PATH="/usr/local/lib/pkgconfig:${PKG_CONFIG_PATH}"
    EXTRA_CMAKE="-DLIBUSB_INCLUDE_DIR=/usr/local/include/libusb-1.0 -DLIBUSB_LIBRARY=/usr/local/lib/libusb-1.0.0.dylib"
    echo "[Info] Using Intel Homebrew paths for x64"
  fi

  echo "[Step] Running cmake for macOS/${CMAKE_ARCH}..."
  echo "  cmake flags: -DBUILD_SHARED_LIBS=ON -DCMAKE_BUILD_TYPE=Release -DCMAKE_OSX_ARCHITECTURES=${CMAKE_ARCH} ${EXTRA_CMAKE}"
  cmake .. \
    -DBUILD_SHARED_LIBS=ON \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_POLICY_VERSION_MINIMUM=3.5 \
    -DCMAKE_DISABLE_FIND_PACKAGE_JpegPkg=ON \
    -DCMAKE_OSX_ARCHITECTURES="${CMAKE_ARCH}" \
    $EXTRA_CMAKE
  echo "[Step] Building with make..."
  make -j$(sysctl -n hw.ncpu 2>/dev/null || echo 4)

  # Fix install names
  echo "[Step] Fixing install names..."
  install_name_tool -id @rpath/libuvc.dylib libuvc.dylib
  # Try both the Cellar path and the opt symlink path
  install_name_tool -change "${LIBUSB_PATH}/libusb-1.0.0.dylib" @loader_path/libusb-1.0.0.dylib libuvc.dylib 2>/dev/null || true
  install_name_tool -change "/opt/homebrew/opt/libusb/lib/libusb-1.0.0.dylib" @loader_path/libusb-1.0.0.dylib libuvc.dylib 2>/dev/null || true
  echo "[Done] Install names fixed"

  OUTPUT_FILE="libuvc.dylib"

elif [ "$PLATFORM" = "linux" ]; then
  # Linux: build as .so
  echo "[Step] Running cmake for Linux/${ARCH}..."
  echo "  cmake flags: -DCMAKE_BUILD_TARGET=Shared -DCMAKE_BUILD_TYPE=Release"
  cmake .. \
    -DCMAKE_BUILD_TARGET=Shared \
    -DCMAKE_BUILD_TYPE=Release \
    -DCMAKE_POLICY_VERSION_MINIMUM=3.5 \
    -DCMAKE_DISABLE_FIND_PACKAGE_JpegPkg=ON
  echo "[Step] Building with make..."
  make -j$(nproc 2>/dev/null || echo 4)

  OUTPUT_FILE="libuvc.so"

elif [ "$PLATFORM" = "win" ]; then
  echo "[Step] Windows build — using MinGW/MSYS2"

  # Determine MSYS2 environment path based on architecture
  # case "$ARCH" in
  #   x64)
  #     MINGW_PREFIX="/mingw64"
  #     MINGW_PACKAGE="mingw-w64-x86_64"
  #     ;;
  #   arm64)
  #     MINGW_PREFIX="/clangarm64"
  #     MINGW_PACKAGE="mingw-w64-clang-aarch64"
  #     ;;
  #   *)
  #     echo "ERROR: Unknown arch: $ARCH"; exit 1
  #     ;;
  # esac
  # echo "[Info] MinGW prefix: $MINGW_PREFIX"
  # echo "[Info] MinGW package prefix: $MINGW_PACKAGE"

  # Install cmake via pacman (MSYS2 package manager)
  # echo "[Step] Installing cmake via pacman..."
  # pacman -S --noconfirm --needed "$MINGW_PACKAGE-cmake" 2>&1 | tail -20

  # Install libusb and jpeg via pacman (MSYS2 package manager)
  # echo "[Step] Installing libusb and jpeg via pacman..."
  # pacman -S --noconfirm --needed "$MINGW_PACKAGE-libusb" "$MINGW_PACKAGE-libjpeg-turbo" 2>&1 | tail -20

  # Run cmake with MSYS Makefiles generator
  echo "[Step] Running cmake for Windows/${ARCH} with MSYS Makefiles..."
  #cmake .. -G "MSYS Makefiles" -D CMAKE_BUILD_TYPE=RelWithDebInfo -D CMAKE_VERBOSE_MAKEFILE:BOOL=ON -D CMAKE_INSTALL_PREFIX=$MINGW_PREFIX .
  cmake .. -G "MSYS Makefiles" -D CMAKE_BUILD_TYPE=RelWithDebInfo -D CMAKE_VERBOSE_MAKEFILE:BOOL=ON
  
  # Copy libusb-1.0.dll.a and libusb.h to build directory for linking
  # echo "[Step] Copying libusb-1.0.dll.a and libusb.h to build directory..."
  cp $MINGW_PREFIX/lib/libusb-1.0.dll.a ./CMakeFiles/uvc.dir/src
  cp $MINGW_PREFIX/include/libusb-1.0/libusb.h ./include/libusb.h
  
  # Build with cmake --build
  echo "[Step] Building with cmake --build..."
  cmake --build .

  find .

  #cd $BUILD_DIR/build_mingw64/
  #/C/msys64/mingw64/bin/cc.exe -O2 -g -DNDEBUG -shared -o libuvc.dll -Wl,--out-implib,libuvc.dll.a -Wl,--major-image-version,0,--minor-image-version,0 -Wl,--whole-archive CMakeFiles/uvc.dir/objects.a -Wl,--no-whole-archive  -lkernel32 -luser32 -lgdi32 -lwinspool -lshell32 -lole32 -loleaut32 -luuid -lcomdlg32 -ladvapi32 -lusb-1.0
  #cd ..
  #cmake --build build_mingw64

  # MinGW outputs libuvc.dll in the build root
  echo "[Step] Locating built DLL..."
  if [ -f "libuvc.dll" ]; then
    OUTPUT_FILE="libuvc.dll"
    echo "[Found] libuvc.dll"
  elif [ -f "Release/libuvc.dll" ]; then
    OUTPUT_FILE="Release/libuvc.dll"
    echo "[Found] Release/libuvc.dll"
  elif [ -f "Release/uvc.dll" ]; then
    echo "[Found] Release/uvc.dll — copying to Release/libuvc.dll"
    cp "Release/uvc.dll" "Release/libuvc.dll"
    OUTPUT_FILE="Release/libuvc.dll"
  else
    echo "ERROR: libuvc.dll not found after build"
    echo "[Debug] Searching for any DLL or LIB files..."
    find . \( -name "libuvc.dll" -o -name "libuvc.lib" -o -name "uvc.dll" -o -name "uvc.lib" \) 2>/dev/null
    exit 1
  fi

else
  echo "ERROR: Unknown platform: $PLATFORM"; exit 1
fi

# Deploy
echo "[Step] Deploying to $OUTPUT_DIR..."
mkdir -p "$OUTPUT_DIR"
cp "$OUTPUT_FILE" "$OUTPUT_DIR/"
echo "[Done] Deployed $(basename $OUTPUT_FILE) ($(stat -f%z "$OUTPUT_DIR/$(basename $OUTPUT_FILE)" 2>/dev/null || stat -c%s "$OUTPUT_DIR/$(basename $OUTPUT_FILE)" 2>/dev/null || echo "?") bytes)"

# Code-sign on macOS
if [ "$PLATFORM" = "macos" ]; then
  echo "[Step] Code-signing dylib..."
  codesign --force --sign - "$OUTPUT_DIR/$(basename $OUTPUT_FILE)"
  echo "[Done] Code-signed"
fi

echo ""
echo "=== Done: ${PLATFORM}/${ARCH} ==="
file "$OUTPUT_DIR/$(basename $OUTPUT_FILE)"

# Cleanup
rm -rf "$BUILD_DIR"