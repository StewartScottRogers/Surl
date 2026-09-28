---
description: Show the Tasks board — what is ready, what is in progress, what is waiting and on what, and what is blocked and on whom.
---
Run
`powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-board.ps1 status`
and summarize it in this order:

1. **Doing.** Nothing should be here outside an active `/task-run`. Anything that is
   was abandoned mid-run: say so, and suggest moving it to `Blocked` or back to
   `Backlog` with a reason.
2. **Ready now,** in the order `/task-run` will take them.
3. **Waiting,** meaning Backlog tasks waiting on dependencies, and on which tasks.
4. **Needs Stewart,** meaning tasks assigned to him, plus `Blocked` tasks whose latest
   `Log` entry names him, each with its reason. Read the task files for the reasons.
5. **Deferred.** The count only, unless the user asks for the list.

Keep it short. The script output already lists everything; the summary is for what
needs attention.
