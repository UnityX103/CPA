#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/../../../.." && pwd)"
SOURCE_FILE="${SCRIPT_DIR}/AppMonitor.m"
OUTPUT_BUNDLE="${PROJECT_ROOT}/Assets/Plugins/macOS/AppMonitor.bundle"
SDK_PATH="$(xcrun --sdk macosx --show-sdk-path)"

clang \
  -fobjc-arc \
  -fvisibility=hidden \
  -O2 \
  -arch arm64 \
  -arch x86_64 \
  -isysroot "${SDK_PATH}" \
  -mmacosx-version-min=10.15 \
  -framework Foundation \
  -framework AppKit \
  -framework ApplicationServices \
  -bundle "${SOURCE_FILE}" \
  -o "${OUTPUT_BUNDLE}"

echo "Built: ${OUTPUT_BUNDLE}"
