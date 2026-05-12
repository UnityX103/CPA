#!/usr/bin/env bash
# 同步本地目录到 bucket 内某个远端路径前缀。
# 用法: sync.sh <local-dir> <remote-prefix> [--purge]
#   --purge：删除远端孤儿文件（默认保留）

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=cdn/uos/_lib.sh
source "$SCRIPT_DIR/_lib.sh"

LOCAL_DIR="${1:-}"
REMOTE_PREFIX="${2:-}"
PURGE="${3:-}"

if [[ -z "$LOCAL_DIR" || -z "$REMOTE_PREFIX" ]]; then
    err "usage: $0 <local-dir> <remote-prefix> [--purge]"
    exit 64
fi
if [[ ! -d "$LOCAL_DIR" ]]; then
    err "local dir does not exist: $LOCAL_DIR"
    exit 66
fi

uas_login_if_needed

EXTRA=()
if [[ "$PURGE" == "--purge" ]]; then
    EXTRA+=(-d)
    log "purge mode: 远端孤儿文件将被删除"
fi

log "syncing '$LOCAL_DIR' -> bucket://$REMOTE_PREFIX"
uas entries sync "$LOCAL_DIR" "$REMOTE_PREFIX" "${COMMON_BUCKET[@]}" --interactive=false "${EXTRA[@]}"
log "sync done"
