#!/usr/bin/env bash
# 一键发布：sync → create release → set badge → 哈希校验。
# 用法: publish.sh [build-target] [--resume] [--verify-only]
#   build-target   默认 StandaloneOSX，远端前缀同名
#   --resume       复用 cdn/.uas-state.json 里的 release_id，跳过 sync + create release，
#                  只重新打 badge 并校验。同次发布中途失败时用。
#   --verify-only  既不 sync 也不动 release/badge，仅按 STATE_FILE 校验
#
# 可选环境:
#   AA_BUILD_DIR=<path>     覆盖 ServerData/AA/<target>
#   REMOTE_PREFIX=<name>    覆盖远端路径前缀（默认等于 build-target）
#   PURGE_REMOTE=1          entries sync 加 -d 清孤儿
#   FORCE_LOGIN=1           清 keychain 缓存后重新 auth login（共享机 / CI 必须）
#   SKIP_PROBE=1            跳过哈希校验（仅测试用，会得到“假阳性”发布）

set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=cdn/uos/_lib.sh
source "$SCRIPT_DIR/_lib.sh"

BUILD_TARGET=""
MODE="full"   # full | resume | verify-only
for arg in "$@"; do
    case "$arg" in
        --resume) MODE="resume" ;;
        --verify-only) MODE="verify-only" ;;
        --*) err "unknown flag: $arg"; exit 64 ;;
        *) [[ -z "$BUILD_TARGET" ]] && BUILD_TARGET="$arg" ;;
    esac
done
BUILD_TARGET="${BUILD_TARGET:-StandaloneOSX}"

DEFAULT_DIR="$PROJECT_ROOT/ServerData/AA/$BUILD_TARGET"
BUILD_DIR="${AA_BUILD_DIR:-$DEFAULT_DIR}"
REMOTE_PREFIX="${REMOTE_PREFIX:-$BUILD_TARGET}"
if [[ "$BUILD_DIR" != /* ]]; then
    BUILD_DIR="$PROJECT_ROOT/$BUILD_DIR"
fi

if [[ ! -d "$BUILD_DIR" ]]; then
    err "AA 输出目录不存在：$BUILD_DIR"
    err "先在 Unity 走 AA 构建（Tools/CPA/HotUpdate/热更新当前平台 或 Addressables 窗口）"
    exit 66
fi
if ! find "$BUILD_DIR" -mindepth 1 -print -quit | grep -q .; then
    err "$BUILD_DIR 为空"
    exit 1
fi

log "mode=$MODE  target=$BUILD_TARGET  bucket=$UAS_BUCKET_ID  badge=$UAS_BADGE"
log "local=$BUILD_DIR"
log "remote prefix=$REMOTE_PREFIX"

uas_login_if_needed
uas config set bucket "$UAS_BUCKET_ID" >/dev/null

# 收集要校验的目标文件
collect_probe_targets() {
    local arr=()
    local f
    for f in "$BUILD_DIR"/catalog_*.bin "$BUILD_DIR"/catalog_*.hash "$BUILD_DIR"/catalog_*.json; do
        [[ -f "$f" ]] && arr+=("$(basename "$f")")
    done
    while IFS= read -r f; do
        [[ -n "$f" ]] && arr+=("$(basename "$f")")
    done < <(find "$BUILD_DIR" -maxdepth 1 -type f -name '*.bundle' | sort)
    printf '%s\n' "${arr[@]}"
}

# 状态文件读写
state_read() {
    [[ -f "$STATE_FILE" ]] && cat "$STATE_FILE" || echo "{}"
}
state_write() {
    local rid="$1" stage="$2"
    cat > "$STATE_FILE" <<EOF
{
  "target": "$BUILD_TARGET",
  "remote_prefix": "$REMOTE_PREFIX",
  "bucket": "$UAS_BUCKET_ID",
  "badge": "$UAS_BADGE",
  "release_id": "$rid",
  "stage": "$stage",
  "ts": "$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
}
EOF
    log "state -> $STATE_FILE  (stage=$stage)"
}

RELEASE_ID=""

case "$MODE" in
    verify-only|resume)
        RELEASE_ID="$(grep -oE '"release_id"[[:space:]]*:[[:space:]]*"[^"]+"' "$STATE_FILE" 2>/dev/null | head -n1 | sed -E 's/.*"release_id"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/')" || true
        if [[ -z "$RELEASE_ID" ]]; then
            err "$MODE 模式需要 $STATE_FILE 里有 release_id，但读不到"
            exit 1
        fi
        log "复用 release_id = $RELEASE_ID"
        ;;
esac

if [[ "$MODE" == "full" ]]; then
    # ── phase 1: sync ──
    if [[ "${PURGE_REMOTE:-0}" == "1" ]]; then
        "$SCRIPT_DIR/sync.sh" "$BUILD_DIR" "$REMOTE_PREFIX" --purge
    else
        "$SCRIPT_DIR/sync.sh" "$BUILD_DIR" "$REMOTE_PREFIX"
    fi
    state_write "" "synced"

    # ── phase 2: release create ──
    NOTES="hotfix $(date '+%Y-%m-%d %H:%M:%S')  target=$BUILD_TARGET  host=$(hostname)"
    RELEASE_ID="$("$SCRIPT_DIR/release.sh" "$NOTES")"
    if [[ -z "$RELEASE_ID" ]]; then
        err "release.sh 返回空 id"
        exit 1
    fi
    state_write "$RELEASE_ID" "release-created"
fi

if [[ "$MODE" != "verify-only" ]]; then
    # ── phase 3: badge ──
    "$SCRIPT_DIR/set_badge.sh" "$UAS_BADGE" "$RELEASE_ID"
    state_write "$RELEASE_ID" "badge-set"
fi

# ── phase 4: 校验 ──
if [[ "${SKIP_PROBE:-0}" == "1" ]]; then
    log "SKIP_PROBE=1 → 跳过哈希校验"
    state_write "$RELEASE_ID" "done-unverified"
    log "DONE (unverified)"
    exit 0
fi

mapfile -t TARGETS < <(collect_probe_targets)
if [[ ${#TARGETS[@]} -eq 0 ]]; then
    err "没找到任何 catalog/bundle 用作校验对象"
    exit 1
fi
log "校验 ${#TARGETS[@]} 个文件 (sha256)"

# 给 badge 在 CDN 上一些生效时间（CDN 缓存层有 5~30s 延迟很常见）
FAILED_FILES=()
for attempt in 1 2 3 4 5 6; do
    FAILED_FILES=()
    for name in "${TARGETS[@]}"; do
        URL="${UAS_LOAD_URL_PREFIX%/}/${REMOTE_PREFIX}/${name}"
        if ! verify_one_file "$BUILD_DIR/$name" "$URL"; then
            FAILED_FILES+=("$name")
        fi
    done
    if [[ ${#FAILED_FILES[@]} -eq 0 ]]; then
        state_write "$RELEASE_ID" "verified"
        log "DONE  release_id=$RELEASE_ID"
        exit 0
    fi
    log "attempt #$attempt: ${#FAILED_FILES[@]} file(s) 校验失败，2s 后重试..."
    sleep 2
done

err "上传 + badge 完成，但 ${#FAILED_FILES[@]} 个文件哈希校验未通过：${FAILED_FILES[*]}"
err "release_id=$RELEASE_ID  已记录到 $STATE_FILE"
err "稍后可单独跑：  cdn/uos/publish.sh $BUILD_TARGET --verify-only"
err "或检查 CDN 缓存策略 / sync 是否漏文件"
exit 2
