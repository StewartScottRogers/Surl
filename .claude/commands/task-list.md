---
description: List the tasks in one state folder of the Surl task board, with optional filters.
argument-hint: <folder: Backlog | Doing | Blocked | Deferred | Done | All> [filters]
---
List tasks on the board. Argument: **$ARGUMENTS**

Read `.claude/skills/task-board/ADMIN.md` if you have not already; it is the reference for
the read-only commands. Nothing in this command changes the board.

1. Work out the folder from the argument. An empty argument means `All`. Accept any
   case, and accept a bare filter with no folder (`-Assignee Stewart`), in which case
   the folder is `All`.
2. Run, adding only the filters the argument actually asked for:
   `powershell -NoProfile -ExecutionPolicy Bypass -File .claude/skills/task-board/task-admin.ps1 list -Folder <folder> [-Assignee Claude|Stewart] [-Priority High|Normal|Low] [-Pipeline feature|protocol|docs|direct] [-NoArchive]`
3. Report the output as it came back. Do not reformat the rows or re-sort them: the queue
   positions are computed the same way `task-board.ps1 next` computes them, and changing
   the order would make them lie.
4. If the folder is `Backlog`, say which task is next in the queue and name anything that
   is waiting on a dependency or on Stewart, since that is what the reader is deciding from.

If the argument names something that is not a folder, say so and list the five folder
names rather than guessing.
