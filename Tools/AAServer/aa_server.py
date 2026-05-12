#!/usr/bin/env python3
"""
本地 Addressables HTTP 文件服务器。

用法：
    python3 aa_server.py              # 默认 9000 端口，托管 ServerData/
    python3 aa_server.py --port 9100  # 自定义端口
    python3 aa_server.py --root path  # 自定义根目录

约定：
- Addressables Profile RemoteLoadPath = http://localhost:9000/AA/[BuildTarget]
- Addressables Profile RemoteBuildPath = [ProjectRoot]/ServerData/AA/[BuildTarget]
- 该脚本默认根目录就是项目根 ServerData/，因此 URL /AA/StandaloneOSX/... 等价
  ServerData/AA/StandaloneOSX/...

注意 CORS：响应里加 Access-Control-Allow-Origin: * 方便 WebGL / 在线编辑器调试。
"""
from __future__ import annotations

import argparse
import http.server
import os
import socketserver
import sys
from pathlib import Path

DEFAULT_PORT = 9000
DEFAULT_ROOT = Path(__file__).resolve().parents[2] / "ServerData"


class _Handler(http.server.SimpleHTTPRequestHandler):
    """关掉缓存 + 加 CORS，避免热重启时浏览器/AA 读到旧 catalog。"""

    def end_headers(self) -> None:
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Cache-Control", "no-store, no-cache, must-revalidate, max-age=0")
        self.send_header("Pragma", "no-cache")
        super().end_headers()

    # 静音 favicon 噪音
    def log_message(self, format: str, *args) -> None:
        if "favicon.ico" in format % args:
            return
        super().log_message(format, *args)


class _ReusableServer(socketserver.ThreadingTCPServer):
    allow_reuse_address = True
    daemon_threads = True


def main() -> int:
    parser = argparse.ArgumentParser(description="Addressables local HTTP server")
    parser.add_argument("--port", type=int, default=DEFAULT_PORT)
    parser.add_argument("--root", type=Path, default=DEFAULT_ROOT)
    args = parser.parse_args()

    root: Path = args.root.resolve()
    root.mkdir(parents=True, exist_ok=True)
    os.chdir(root)

    print(f"[AA] root = {root}")
    print(f"[AA] listening on http://localhost:{args.port}/")
    print(f"[AA] try: curl -I http://localhost:{args.port}/AA/StandaloneOSX/catalog_*.json")
    print("[AA] Ctrl-C to stop.")

    try:
        with _ReusableServer(("", args.port), _Handler) as srv:
            srv.serve_forever()
    except KeyboardInterrupt:
        print("\n[AA] bye.")
        return 0
    except OSError as exc:
        print(f"[AA] failed to bind :{args.port} — {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
