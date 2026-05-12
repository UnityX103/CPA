#!/usr/bin/env bash
# 在当前 bucket 内容上创建一个 release，把 release id 写到 stdout。
# 用法: release.sh [notes]

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=cdn/uos/_lib.sh
source "$SCRIPT_DIR/_lib.sh"

NOTES="${1:-hotfix $(date '+%Y-%m-%d %H:%M:%S')}"

uas_login_if_needed

log "creating release on bucket $UAS_BUCKET_ID" >&2

OUTPUT="$("$UAS_BIN" releases create \
    "${COMMON_BUCKET[@]}" \
    --notes "$NOTES" \
    --interactive=false 2>&1 || true)"

echo "$OUTPUT" >&2

RELEASE_ID="$(echo "$OUTPUT" | tr -d '\r' | grep -oE '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}' | head -n1)"

if [[ -z "$RELEASE_ID" ]]; then
    # 回退：再 list 一次取最近一条
    RELEASE_ID="$("$UAS_BIN" releases list "${COMMON_BUCKET[@]}" --interactive=false 2>/dev/null \
                  | grep -oE '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}' | head -n1 || true)"
fi
if [[ -z "$RELEASE_ID" ]]; then
    err "无法从 CLI 输出解析 release id"
    exit 1
fi

log "release id = $RELEASE_ID" >&2
echo "$RELEASE_ID"
