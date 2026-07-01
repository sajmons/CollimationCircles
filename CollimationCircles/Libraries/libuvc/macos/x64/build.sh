#!/bin/bash
# Build libuvc.dylib for macOS/x64 (Intel)
# Prerequisites on arm64 Macs: install Intel Homebrew + libusb:
#   arch -x86_64 /bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"
#   arch -x86_64 /usr/local/bin/brew install libusb
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
exec "$SCRIPT_DIR/../build-libuvc-common.sh" macos x64 "$SCRIPT_DIR"