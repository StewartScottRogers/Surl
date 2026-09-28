---
description: Validate the Surl task board - IDs, front matter, dependencies, cycles, sections.
---
Validate the board.

1. Run:
   `powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-admin.ps1 check`
   Add `-FailOnProblem` when the caller wants a non-zero exit code for a hook or a build step.
2. It checks: duplicate or missing IDs, an id that disagrees with its file name, unknown
   `priority` / `assignee` / `pipeline` values, a missing `requirement` or a malformed
   `created`, a dependency that is not on the board, a task depending on itself, a
   dependency cycle, a `Done` task with an unticked box or no completion date, an
   unfinished task carrying a completion date, a `Doing` task whose dependencies are not
   done, more than one task in `Doing`, a missing required section, content after
   `## Log`, an unfinished task with no acceptance criteria, and gaps in the ID sequence.
3. Report every problem. Do not fix anything as part of this command: a problem is either
   a task that needs editing or a rule that needs changing, and which one it is matters.
4. If a finding looks like the checker is wrong rather than the board, say so — that has
   happened before, and a false positive in a validator is worse than no validator.

Do not work around a finding by editing the checker to stop reporting it, unless the rule
itself is genuinely wrong and you say why.
