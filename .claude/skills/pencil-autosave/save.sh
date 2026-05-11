#!/usr/bin/env bash
# 切换焦点到 Pencil.app，发送 ⌘S 保存当前活动 .pen 文件，然后尽量切回原前台 app。
#
# 失败兜底：keystroke 拒绝时仍把 Pencil 拉到前台，提示用户手按 ⌘S，
# 并打印「需要授权给哪个 app」的具体指引。
#
# 用法:
#   bash .claude/skills/pencil-autosave/save.sh
#
# 退出码:
#   0  保存命令已发送
#   2  Pencil 未安装 / 无法激活
#   3  Accessibility 权限缺失（已拉起 Pencil 让用户手按 ⌘S）

# 不开 set -u —— 历史进程查找里可能拿到空字符串

# 1. 先 activate Pencil（这步无需 Accessibility 权限）
activate_out=$(osascript -e 'tell application "Pencil" to activate' 2>&1)
if [ $? -ne 0 ]; then
    echo "ERROR: 无法激活 Pencil.app —— 是否已安装？" >&2
    echo "$activate_out" >&2
    exit 2
fi

# 2. 尝试 keystroke ⌘S（需要 Accessibility 权限）
keystroke_out=$(osascript <<'APPLESCRIPT' 2>&1
tell application "System Events"
    set priorApp to name of first application process whose frontmost is true
end tell
delay 0.20
tell application "System Events"
    keystroke "s" using command down
end tell
delay 0.35
try
    tell application priorApp to activate
end try
return "pencil-autosave: saved. prior_app=" & priorApp
APPLESCRIPT
)
keystroke_code=$?

if [ $keystroke_code -eq 0 ]; then
    echo "$keystroke_out"
    exit 0
fi

# 3. keystroke 失败 —— 找责任 app（顶层 GUI）+ 打印权限指引
top_app=""
pid=$$
for _ in 1 2 3 4 5 6 7 8 9 10; do
    pid=$(ps -o ppid= -p "$pid" 2>/dev/null | tr -d ' ')
    [ -z "$pid" ] && break
    [ "$pid" = "0" ] && break
    [ "$pid" = "1" ] && break
    cur=$(ps -o comm= -p "$pid" 2>/dev/null)
    case "$cur" in
        */iTerm.app/Contents/MacOS/iTerm2) top_app="iTerm"; break ;;
        */Terminal.app/Contents/MacOS/Terminal) top_app="Terminal"; break ;;
        */Warp.app/*) top_app="Warp"; break ;;
        */Hyper.app/*) top_app="Hyper"; break ;;
        */Ghostty.app/*) top_app="Ghostty"; break ;;
        */Visual\ Studio\ Code.app/*) top_app="Visual Studio Code"; break ;;
        */Cursor.app/*) top_app="Cursor"; break ;;
    esac
done
[ -z "$top_app" ] && top_app="你的终端 App"

{
    echo "WARN: 自动 ⌘S 失败（osascript 没有 Accessibility 权限）。"
    echo "      已把 Pencil 拉到前台，请手按 ⌘S 完成本次保存。"
    echo ""
    echo "原始错误：$keystroke_out"
    echo ""
    echo "永久修复 —— 直接给 osascript 二进制授权（最可靠，不挂在终端 app 上）："
    echo "  1. 已自动打开 System Settings → Accessibility 面板"
    echo "  2. 点左下角 + 号 → 在文件选择器按 ⌘+Shift+G"
    echo "  3. 输入 /usr/bin/osascript 回车 → 选中 → 打开 → 勾选"
    echo "  4. 重跑 bash .claude/skills/pencil-autosave/save.sh 验证"
    echo ""
    echo "（备选）也可勾「$top_app」，但视 Claude Code 的责任 app 链条而定，可能不生效。"
} >&2

# 自动打开 System Settings → Privacy & Security → Accessibility
open "x-apple.systempreferences:com.apple.preference.security?Privacy_Accessibility" 2>/dev/null || true
exit 3
