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

# CLI 输出形如：
#   Created release (#2) in bucket (205577ad-…).
#     Id: dc707be9-666d-44df-a1a5-20fe0bdbbecd
#     Created: ...
# 必须严格抓 "Id:" 行后面的 UUID，否则会误拿 bucket id
RELEASE_ID="$(echo "$OUTPUT" | tr -d '\r' \
    | grep -Ei '^[[:space:]]*Id[[:space:]]*:' \
    | head -n1 \
    | grep -oE '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}' \
    | head -n1)"

if [[ -z "$RELEASE_ID" ]]; then
    # 回退：再 list 一次取最近一条（同样按 Id: 行抓）
    RELEASE_ID="$("$UAS_BIN" releases list "${COMMON_BUCKET[@]}" --interactive=false 2>/dev/null \
                  | tr -d '\r' \
                  | grep -Ei '^[[:space:]]*Id[[:space:]]*:' \
                  | head -n1 \
                  | grep -oE '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}' \
                  | head -n1 || true)"
fi
if [[ -z "$RELEASE_ID" ]]; then
    err "无法从 CLI 输出解析 release id"
    exit 1
fi

log "release id = $RELEASE_ID" >&2
echo "$RELEASE_ID"
