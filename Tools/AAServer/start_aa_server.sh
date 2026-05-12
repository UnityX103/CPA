#!/usr/bin/env bash
# 启动本地 Addressables HTTP 服务器（默认 9000 端口，托管项目根 ServerData/）。
# 使用：./Tools/AAServer/start_aa_server.sh [PORT]
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PORT="${1:-9000}"
exec python3 "$HERE/aa_server.py" --port "$PORT"
