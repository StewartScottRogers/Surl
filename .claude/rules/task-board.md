---
paths:
  - "Tasks/**"
---
# Task board rules

- Read `.claude/skills/task-board/SKILL.md` before creating, moving or editing a task.
  It is binding.
- The folder is the status. Move tasks only with
  `.claude/skills/task-board/task-board.ps1 move`, never with `Move-Item`, `git mv` or
  an editor.
- Create tasks only with `task-board.ps1 new`, so the ID comes from the script.
- Never edit anything inside `Tasks/Done/<timestamp>/`. Archived tasks are final.
- `## Log` is the last section and append-only. Absolute dates only.
