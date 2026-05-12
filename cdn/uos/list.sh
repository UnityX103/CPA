#!/usr/bin/env bash
# 列出当前 bucket 的 release。
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=cdn/uos/_lib.sh
source "$SCRIPT_DIR/_lib.sh"

uas_login_if_needed
uas releases list "${COMMON_BUCKET[@]}" --interactive=false
