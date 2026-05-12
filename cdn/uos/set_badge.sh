#!/usr/bin/env bash
# 给 release 打 badge（已存在则改用 update）。
# 用法: set_badge.sh <badge-name> <release-id>

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=cdn/uos/_lib.sh
source "$SCRIPT_DIR/_lib.sh"

BADGE_NAME="${1:-}"
RELEASE_ID="${2:-}"

if [[ -z "$BADGE_NAME" || -z "$RELEASE_ID" ]]; then
    err "usage: $0 <badge-name> <release-id>"
    exit 64
fi

uas_login_if_needed

# `latest` 是 UOS 保留 badge，自动跟最新 release，不允许手动 add/remove
if [[ "$BADGE_NAME" == "latest" ]]; then
    log "badge=latest 是 UOS 保留名（创建 release 时自动指向最新一条），跳过手动绑定"
    exit 0
fi

log "tag release '$RELEASE_ID' with badge '$BADGE_NAME'"
# CLI 没有 badges update：badge 已存在时 badges add 会失败，需要先 remove 再 add
if ! uas badges add "$BADGE_NAME" "$RELEASE_ID" "${COMMON_BUCKET[@]}" --interactive=false; then
    log "badge add 失败（多半是 badge 已存在），先 remove 再 add"
    uas badges remove "$BADGE_NAME" "${COMMON_BUCKET[@]}" --interactive=false || true
    uas badges add "$BADGE_NAME" "$RELEASE_ID" "${COMMON_BUCKET[@]}" --interactive=false
fi
log "badge set"
