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

log "tag release '$RELEASE_ID' with badge '$BADGE_NAME'"
if ! uas badges add "$BADGE_NAME" "$RELEASE_ID" "${COMMON_BUCKET[@]}" --interactive=false 2>/dev/null; then
    log "badge add 失败，尝试 badges update（badge 已存在）"
    uas badges update "$BADGE_NAME" "$RELEASE_ID" "${COMMON_BUCKET[@]}" --interactive=false
fi
log "badge set"
