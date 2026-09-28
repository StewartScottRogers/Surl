---
description: Plan a request into tasks on the Tasks board — decomposed, dependency-ordered, and filed in Backlog. Starts nothing.
argument-hint: <what needs doing, e.g. "support --max-time and --connect-timeout">
---
Plan this into tasks: **$ARGUMENTS**

1. Delegate to `task-planner`, giving it the request above verbatim.
2. If it comes back with a clarifying question instead of tasks, put the question to
   the user and stop.
3. Otherwise run
   `powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-board.ps1 status`
   and report:
   - the planner's table of new tasks;
   - any questions or decisions it filed for Stewart;
   - which task `/task-run` will take first.

Do not start any task. Planning and doing are separate commands so the user can
review, reorder or edit the plan before any code is written.
