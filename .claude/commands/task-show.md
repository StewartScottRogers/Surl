---
description: Show one Surl task in full, with its dependency and readiness position.
argument-hint: <task id, e.g. BL-008>
---
Show task **$ARGUMENTS**.

1. Run:
   `powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-admin.ps1 show -Id $ARGUMENTS`
2. Relay the header block and the task body. The header carries what the file itself does
   not: which dependencies are not yet `Done`, which tasks are waiting on this one, how
   many acceptance boxes are ticked, and the queue position if it is ready.
3. If the task is archived, say so plainly — archived tasks are final, and reopening
   finished work means filing a new task, not editing that file.

If no id is given, run `task-admin.ps1 list -Folder Doing` and `list -Folder Backlog
-NoArchive` instead, and ask which one they meant.
