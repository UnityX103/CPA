## REQUIRED: Task Lifecycle Commands
You MUST run these commands. Do NOT skip any step.

1. Claim your task:
   omc team api claim-task --input '{"team_name":"team-codex-executor-worker-tea","task_id":"3","worker":"worker-3"}' --json
   Save the claim_token from the response.
2. Do the work described below.
3. On completion (use claim_token from step 1):
   omc team api transition-task-status --input '{"team_name":"team-codex-executor-worker-tea","task_id":"3","from":"in_progress","to":"completed","claim_token":"<claim_token>"}' --json
4. On failure (use claim_token from step 1):
   omc team api transition-task-status --input '{"team_name":"team-codex-executor-worker-tea","task_id":"3","from":"in_progress","to":"failed","claim_token":"<claim_token>"}' --json
5. ACK/progress replies are not a stop signal. Keep executing your assigned or next feasible work until the task is actually complete or failed, then transition and exit.

## Task Assignment
Task ID: 3
Worker: worker-3
Subject: Worker 3: 你是桌宠多人番茄钟实施 team 的其中一个 codex executor worker (team-name: pomodoro-mp-i

你是桌宠多人番茄钟实施 team 的其中一个 codex executor worker (team-name: pomodoro-mp-impl)。

总方案: /Users/xpy/Desktop/NanZhai/CPA/.omc/plans/multiplayer-pomodoro-plan.md (已通过 Architect 最终审核 APPROVED)

## 启动流程 (严格按序执行)

1. 先 cd 到 /Users/xpy/Desktop/NanZhai/CPA
2. 执行 'omc team api list-tasks --input '{"team_name":"pomodoro-mp-impl"}' --json' 列出可用任务
3. 选择状态为 OPEN 的任务, 执行 'omc team api claim-task --input '{"team_name":"pomodoro-mp-impl","task_id":"<id>","worker":"<your worker name>","expected_version":<version>}' --json' 认领
4. 认领成功后 transition 任务状态到 IN_PROGRESS: 'omc team api transition-task-status --input '{"team_name":"pomodoro-mp-impl","task_id":"<id>","to_status":"IN_PROGRESS","worker":"<your worker>"}''
5. 任务描述里会给出一个 .md 文件路径, 读取该文件, 里面有完整的子任务规格 (文件所有权、实施要点、验收标准)
6. 严格按照子任务规格实施, 只写入规格允许的文件路径
7. 完成后运行验收脚本 (node --test 或 Unity 编译检查), 通过后将任务状态 transition 到 DONE 并通过 mailbox 发送完成摘要给 leader-fixed

## 关键规则

- **严禁** 写入不属于你的任务的文件 (文件所有权冲突会直接破坏同伴的工作)
- **严禁** 修改 /Users/xpy/Desktop/NanZhai/CPA/.omc/plans/multiplayer-pomodoro-plan.md (方案文件只读)
- 遇到需要其他 worker 的产物时 (Worker 3 UI 层依赖 Worker 2 网络层符号), 先完成不依赖他人的部分, 再通过 'omc team api send-message' 向目标 worker 发 mailbox 协调
- 所有测试必须通过后再 transition DONE
- 项目 CLAUDE.md 在 /Users/xpy/Desktop/NanZhai/CPA/CLAUDE.md, 含 QFramework 规范, 实施时必须符合
- 回答和代码注释使用中文, 代码标识符用英文
- 项目使用 Unity 6 + QFramework v1.0, 方案细节见 .omc/plans/multiplayer-pomodoro-plan.md
- 同一时间只能认领一个任务, 完成后如有剩余 OPEN 任务可再认领

开始吧.

REMINDER: You MUST run transition-task-status before exiting. Do NOT write done.json or edit task files directly.