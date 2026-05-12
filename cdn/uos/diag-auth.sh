#!/usr/bin/env bash
# 诊断 UOS 鉴权失败真因。可单独跑，不依赖 publish.sh。
set -uo pipefail

PROJECT_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
CDN_DIR="$PROJECT_ROOT/cdn"
CRED_FILE="$CDN_DIR/.uas-credentials.env"

if [[ -x "$CDN_DIR/uos/uas" ]]; then UAS="$CDN_DIR/uos/uas"
elif [[ -x "$CDN_DIR/uas" ]]; then UAS="$CDN_DIR/uas"
elif [[ -x "$CDN_DIR/uos/uas_mac" ]]; then UAS="$CDN_DIR/uos/uas_mac"
else echo "no uas binary" >&2; exit 2; fi

# shellcheck disable=SC1090
set -a; source "$CRED_FILE"; set +a

# 兼容旧名：早期 env 文件可能没有 UAS_APP_SECRET，set -u 下访问会炸
UAS_APP_SECRET="${UAS_APP_SECRET:-}"

mask() {
    local s="$1"
    if [[ -z "$s" ]]; then echo "<empty>"; return; fi
    if [[ ${#s} -lt 8 ]]; then echo "<too short>"; return; fi
    echo "${s:0:4}…${s: -4}  (len=${#s})"
}

printf 'UAS binary=%s\nUAS_APP_ID=%s\nUAS_APP_SECRET=%s\nUAS_APP_SERVICE_SECRET=%s\nUAS_BUCKET_ID=%s\n\n' \
    "$UAS" "${UAS_APP_ID:-<missing>}" "$(mask "$UAS_APP_SECRET")" \
    "$(mask "${UAS_APP_SERVICE_SECRET:-}")" "${UAS_BUCKET_ID:-<missing>}"

if [[ -n "$UAS_APP_SECRET" ]]; then
    printf '=== 1. auth login  --uos_app_secret（CLI v1.0.9 已废弃，FoodBattle 老路径） ===\n'
    "$UAS" --verbose auth login \
        --uos_app_id "$UAS_APP_ID" \
        --uos_app_secret "$UAS_APP_SECRET" \
        --interactive=false 2>&1 || true
else
    printf '=== 1. UAS_APP_SECRET 未设置 → 跳过废弃路径测试 ===\n'
fi

printf '\n=== 2. auth login  --uos_app_service_secret（正式路径）===\n'
"$UAS" --verbose auth login \
    --uos_app_id "$UAS_APP_ID" \
    --uos_app_service_secret "$UAS_APP_SERVICE_SECRET" \
    --interactive=false 2>&1 || true

printf '\n=== 3. curl 探测 UOS / API 可达性 ===\n'
for HOST in \
    "https://uos.unity.cn" \
    "https://a.unity.cn"; do
    echo "-> $HOST"
    curl -sS -o /dev/null -D - --max-time 5 "$HOST" 2>&1 | head -3
    echo
done

printf '\n=== 4. CDN release_by_badge 端点（公开访问，无须鉴权）===\n'
curl -sS -o /tmp/uos_probe.bin -D /tmp/uos_probe.h -w 'http=%{http_code}\nsize=%{size_download}\n' \
    "https://a.unity.cn/client_api/v1/buckets/${UAS_BUCKET_ID}/release_by_badge/${UAS_BADGE:-latest}/content/StandaloneOSX/catalog_0.1.bin" 2>&1 || true
echo
echo "-- headers --"
[[ -f /tmp/uos_probe.h ]] && head -10 /tmp/uos_probe.h
echo "-- body head --"
[[ -f /tmp/uos_probe.bin ]] && head -c 300 /tmp/uos_probe.bin
echo

rm -f /tmp/uos_probe.bin /tmp/uos_probe.h
printf '\n=== done ===\n'
