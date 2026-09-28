---
description: Archive finished tasks from Tasks/Done into a new timestamped folder.
argument-hint: [days] — archive tasks completed at least this many days ago (0 archives all of Done); blank archives all of Done once it holds more than 20
---
Archive the task board's `Done` folder. Days: **$ARGUMENTS**

If days is blank, run
`powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-board.ps1 archive -WhenDoneIsLong`,
which archives all of `Done` once it holds more than 20 tasks and otherwise does nothing.
If days is given, run
`powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-board.ps1 archive -OlderThanDays <days>`.

Report the folder it created and the task IDs it moved, or why nothing was archived.
Pass on any warning about a task with no `completed` date.

Archived tasks are final: never edit a file inside `Tasks/Done/<timestamp>/`
afterwards. They still count as done for other tasks' dependencies.
