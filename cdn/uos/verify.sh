#!/usr/bin/env bash
# 独立 verify：对比 release_by_badge URL 内容与本地 AA 输出的 sha256。
# 用法: verify.sh [build-target]
#   不操作 release / badge，纯只读校验。publish.sh --verify-only 会落到这里。

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=cdn/uos/_lib.sh
source "$SCRIPT_DIR/_lib.sh"

BUILD_TARGET="${1:-StandaloneOSX}"
REMOTE_PREFIX="${REMOTE_PREFIX:-$BUILD_TARGET}"
BUILD_DIR="${AA_BUILD_DIR:-$PROJECT_ROOT/ServerData/AA/$BUILD_TARGET}"
if [[ "$BUILD_DIR" != /* ]]; then
    BUILD_DIR="$PROJECT_ROOT/$BUILD_DIR"
fi

URL_PREFIX="${UAS_LOAD_URL_PREFIX%/}/$REMOTE_PREFIX"
log "URL prefix=$URL_PREFIX"

if [[ ! -d "$BUILD_DIR" ]]; then
    err "AA build dir not found: $BUILD_DIR"
    exit 66
fi

CANDIDATES=()
for f in "$BUILD_DIR"/catalog_*.bin "$BUILD_DIR"/catalog_*.hash "$BUILD_DIR"/catalog_*.json; do
    [[ -f "$f" ]] && CANDIDATES+=("$(basename "$f")")
done
while IFS= read -r f; do
    [[ -n "$f" ]] && CANDIDATES+=("$(basename "$f")")
done < <(find "$BUILD_DIR" -maxdepth 1 -type f -name '*.bundle' | sort)

if [[ ${#CANDIDATES[@]} -eq 0 ]]; then
    err "no verification candidates under $BUILD_DIR"
    exit 1
fi

OK=1
for name in "${CANDIDATES[@]}"; do
    URL="$URL_PREFIX/$name"
    LOCAL="$BUILD_DIR/$name"
    verify_one_file "$LOCAL" "$URL" || OK=0
done

if [[ $OK -eq 1 ]]; then
    log "verify PASSED for $REMOTE_PREFIX"
    exit 0
fi
err "verify FAILED — see lines above"
exit 1
