#!/usr/bin/env bash
# 切换焦点到 Pencil.app，发送 ⌘S 保存当前活动 .pen 文件，然后尽量切回原前台 app。
#
# 需要给运行 Claude Code 的终端/进程授予 macOS 辅助功能（Accessibility）权限：
# System Settings → Privacy & Security → Accessibility → 勾选你的终端/Claude Code.app
#
# 用法:
#   bash .claude/skills/pencil-autosave/save.sh
#
# 退出码:
#   0  保存命令已成功发送（不等于 Pencil 确实落盘，由 Pencil 自己决定）
#   非0 osascript 失败（通常是权限或 Pencil 未安装）

set -eu

OSASCRIPT_OUT=$(osascript <<'APPLESCRIPT'
-- 1. 记录当前前台 app
tell application "System Events"
    set priorApp to name of first application process whose frontmost is true
end tell

-- 2. 激活 Pencil
try
    tell application "Pencil" to activate
on error errMsg
    error "无法激活 Pencil.app: " & errMsg
end try

-- 3. 等焦点切过去（Pencil 接管主线程需要一点时间）
delay 0.25

-- 4. 发送 Cmd+S
tell application "System Events"
    keystroke "s" using command down
end tell

-- 5. 等保存完成（Pencil 序列化 .pen 可能要一两百毫秒）
delay 0.35

-- 6. 尝试切回原前台 app（失败不中断，至少保证保存成功）
try
    tell application priorApp to activate
end try

return "pencil-autosave: saved. prior_app=" & priorApp
APPLESCRIPT
)

echo "$OSASCRIPT_OUT"
