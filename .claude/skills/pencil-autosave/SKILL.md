---
name: pencil-autosave
description: Persist changes to the currently active Pencil .pen document without user intervention — activates Pencil.app, sends ⌘S, restores prior app focus. Use immediately after any mcp__pencil__batch_design that mutates the document (insert/copy/update/replace/move/delete/image), or when the user explicitly asks to "保存 Pencil" / "落盘" / "⌘S". Prerequisite: macOS Accessibility permission granted to the terminal / Claude Code process running the bash command.
---

# Pencil Autosave

用一个 macOS AppleScript 把焦点切到 Pencil，发 `⌘S`，再切回来，实现 Pencil 文档"无人值守"落盘。

## 何时使用

- 刚调用 `mcp__pencil__batch_design` 做了会**修改** .pen 的操作：`insert` / `copy` / `update` / `replace` / `move` / `delete` / `image`
- 用户明确说"保存 Pencil"、"⌘S"、"同步到 .pen"、"落盘"
- 一组连续的 `batch_design` 做完（末尾一次性 save 即可；每条都调会频繁抢焦点）

## 不使用

- 只读操作：`batch_get` / `get_editor_state` / `get_screenshot` / `search_all_unique_properties` —— 不修改文档，无需保存
- Pencil 根本没打开时（先确认 `get_editor_state` 返回一个 active editor，才谈得上保存）

## 如何执行

单条 bash 命令：

```bash
bash .claude/skills/pencil-autosave/save.sh
```

期望输出：`pencil-autosave: saved. prior_app=<上一次的前台 app 名>`

## 脚本做的事（流程）

1. `tell application "System Events"` 记录当前前台 app 名
2. `tell application "Pencil" to activate` 把 Pencil 拉到前台
3. `delay 0.25` 等焦点切过去（否则 keystroke 会落到原 app）
4. `keystroke "s" using command down` 发送 `⌘S`
5. `delay 0.35` 等 Pencil 序列化 .pen 落盘
6. 尝试切回原前台 app（失败不终止，保存已成功）

## 常见失败

| 症状 | 原因 | 处理 |
|---|---|---|
| `System Events got an error: ... is not allowed to send keystrokes` 或 `(-1719)` / `(-1743)` | 终端/Claude Code 没有辅助功能权限 | 提示用户到 **System Settings → Privacy & Security → Accessibility**，勾选他的终端（Terminal / iTerm / Warp / Claude Code.app），勾上后重跑一次 |
| `Can't find application "Pencil"` | Pencil.app 不在 `/Applications/`，或进程名不是"Pencil" | 用 `osascript -e 'tell application "System Events" to get name of every process'` 查真实名字，改脚本中的 `"Pencil"` |
| `⌘S` 发出去了但 Pencil 没保存 | Pencil 内部有未确认的弹窗拦截，或当前编辑器聚焦在非主画布控件 | 先调 `mcp__pencil__get_editor_state` 看 `Currently active editor`；若无 active editor 说明 Pencil 无可保存文档 |
| 保存后焦点没回到终端 | 原前台 app 名里有特殊字符（如中文、空格）导致 `tell application <name> to activate` 失败 | 可忽略——保存已成功，只是要用户手动切回，或者你下条命令仍能在后台跑 |

## 验证

调完 save.sh 后，不必立刻再验证——osascript 成功返回就意味着 `⌘S` 已经发到 Pencil。如需强验证：

- `git status AUI/PUI.pen` 若有 modified 说明 Pencil 确实写盘了
- `stat -f "%m" AUI/PUI.pen` 对比前后 mtime

## 可选：作为 PostToolUse hook 自动触发

如果不想每次都手动调 skill，可以在 `.claude/settings.local.json` 里加 hook，每次 `mcp__pencil__batch_design` 成功后自动触发：

```jsonc
"hooks": {
  "PostToolUse": [
    {
      "matcher": "mcp__pencil__batch_design",
      "hooks": [
        { "type": "command", "command": "bash .claude/skills/pencil-autosave/save.sh" }
      ]
    }
  ]
}
```

⚠️ 副作用：只读 `batch_design`（少见但可构造——比如纯查询性 `U()` 空 ops）也会触发保存，算是幂等写；但每条 batch_design 都会闪一次 Pencil 焦点。做成 skill 手动调通常更可控。
