#!/bin/bash
# Build libuvc.so for Linux/x64
# Prerequisites: sudo apt install libusb-1.0-0-dev cmake
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
exec "$SCRIPT_DIR/../build-libuvc-common.sh" linux x64 "$SCRIPT_DIR"