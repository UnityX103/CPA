#!/usr/bin/env bash
# 通用底座：解析 CLI 二进制 + 加载凭据 + 暴露 uas() / 鉴权封装。
# 凭据约定与 cdn/.uas-credentials.env 一致：UAS_APP_ID / UAS_APP_SERVICE_SECRET /
# UAS_BUCKET_ID / UAS_BADGE / UAS_LOAD_URL_PREFIX。UAS_APP_SECRET 仅诊断脚本可选。

set -euo pipefail

export LESS="-X"
export PAGER=cat
export GIT_PAGER=cat

LIB_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$LIB_DIR/../.." && pwd)"
CDN_DIR="$PROJECT_ROOT/cdn"
CRED_FILE="$CDN_DIR/.uas-credentials.env"
STATE_FILE="$CDN_DIR/.uas-state.json"

if [[ ! -f "$CRED_FILE" ]]; then
    echo "[uos] missing credentials: $CRED_FILE" >&2
    echo "[uos] cp cdn/.uas-credentials.env.example cdn/.uas-credentials.env  然后填值" >&2
    exit 2
fi
# shellcheck disable=SC1090
set -a; source "$CRED_FILE"; set +a

: "${UAS_APP_ID:?UAS_APP_ID missing}"
: "${UAS_APP_SERVICE_SECRET:?UAS_APP_SERVICE_SECRET missing}"
: "${UAS_BUCKET_ID:?UAS_BUCKET_ID missing}"
: "${UAS_BADGE:=latest}"
: "${UAS_LOAD_URL_PREFIX:=https://a.unity.cn/client_api/v1/buckets/${UAS_BUCKET_ID}/release_by_badge/${UAS_BADGE}/content}"

# 选二进制：mac arm64 > mac x86_64 > windows
case "${OSTYPE:-}" in
    msys*|win32*|cygwin*)
        UAS_BIN="$LIB_DIR/uas.exe"
        ;;
    darwin*|linux*|*)
        if [[ -x "$LIB_DIR/uas" ]]; then
            UAS_BIN="$LIB_DIR/uas"
        elif [[ -x "$CDN_DIR/uas" ]]; then
            UAS_BIN="$CDN_DIR/uas"
        elif [[ -x "$LIB_DIR/uas_mac" ]]; then
            UAS_BIN="$LIB_DIR/uas_mac"
        else
            echo "[uos] no uas binary in $LIB_DIR or $CDN_DIR" >&2
            exit 2
        fi
        ;;
esac

if [[ ! -x "$UAS_BIN" ]]; then
    echo "[uos] uas CLI not executable: $UAS_BIN" >&2
    exit 2
fi

COMMON_BUCKET=(--bucket "$UAS_BUCKET_ID")

uas() { "$UAS_BIN" "$@"; }
log() { printf '[uos] %s\n' "$*"; }
err() { printf '[uos][err] %s\n' "$*" >&2; }

# 鉴权：默认懒登录（auth info 通过就跳过）。
# FORCE_LOGIN=1 时先 auth logout 再 login，强制把 keychain 缓存换成当前 .env 的身份。
#   shared machine / CI / 多 bucket 切换场景务必置 1，避免拿旧缓存发到错地方。
uas_login_if_needed() {
    # 同一发布流水线内（publish.sh → sync.sh / release.sh / set_badge.sh），
    # 一旦登录过就标记并 export，子脚本继承后直接跳过重登
    if [[ "${UAS_AUTH_DONE:-0}" == "1" ]]; then
        return 0
    fi
    if [[ "${FORCE_LOGIN:-0}" == "1" ]]; then
        log "FORCE_LOGIN=1 → 先 auth logout 清缓存"
        "$UAS_BIN" auth logout >/dev/null 2>&1 || true
    elif "$UAS_BIN" auth info >/dev/null 2>&1; then
        log "auth: 已有凭据缓存（如换过 .env 请 FORCE_LOGIN=1 重跑）"
        export UAS_AUTH_DONE=1
        return 0
    fi
    log "auth login ..."
    if ! "$UAS_BIN" auth login \
            --uos_app_id "$UAS_APP_ID" \
            --uos_app_service_secret "$UAS_APP_SERVICE_SECRET" \
            --interactive=false; then
        cat >&2 <<EOF
[uos][err] uas auth login 失败（Invalid credentials）
  排查清单：
    1) cdn/.uas-credentials.env 里 UAS_APP_ID / UAS_APP_SERVICE_SECRET 是否最新；
    2) UOS 控制台 (https://uos.unity.cn → 内容分发 → 应用密钥) 看 Service Secret 是否还有效；
    3) 该 Service Secret 对应的应用是否被授权访问 bucket $UAS_BUCKET_ID。
EOF
        return 1
    fi
    export UAS_AUTH_DONE=1
    # 不让 FORCE_LOGIN 把子脚本拖回 logout/login 路径
    export FORCE_LOGIN=0
}

# 比对一份本地文件与远端 release_by_badge URL 的 sha256，相同退 0。
# 用法: verify_one_file <local-path> <url>
verify_one_file() {
    local local_path="$1"
    local url="$2"
    local tmp="/tmp/uos_verify.$$.$RANDOM"
    local http
    # -L 必须：UOS CDN 把 a.unity.cn 上的 release_by_badge URL 307 重定向到 a2.unity3dcloud.cn 拿字节
    http="$(curl -sSL -o "$tmp" -w '%{http_code}' "$url" || true)"
    if [[ "$http" != "200" ]]; then
        err "  http=$http  $url"
        rm -f "$tmp"
        return 1
    fi
    local r l
    r="$(shasum -a 256 "$tmp" | awk '{print $1}')"
    l="$(shasum -a 256 "$local_path" | awk '{print $1}')"
    rm -f "$tmp"
    if [[ "$r" != "$l" ]]; then
        err "  hash mismatch  $(basename "$local_path")  local=$l  remote=$r"
        return 1
    fi
    log "  ok  $(basename "$local_path")  sha256=$r"
    return 0
}
