---
id: BL-1791
title: Fix dispatch-workflow.ps1 failing to find the run it started under Windows PowerShell 5.1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [.claude/skills/task-board/dispatch-workflow.ps1]
requirement: none
created: 2026-11-26
completed:
---
# BL-1791 — Fix dispatch-workflow.ps1 failing to find the run it started under Windows PowerShell 5.1

## Goal

`dispatch-workflow.ps1`, run with `powershell` as the lanes run it, prints its `run:<id>` line for
the run it dispatched.

## Context

On 2026-11-26 BL-1789 ran `powershell -NoProfile -ExecutionPolicy Bypass -File
.claude/skills/task-board/dispatch-workflow.ps1 -Id BL-1789 -Workflow release -Ref factory/phase-1`.
It dispatched run 45836201806 and counted it, then exited 1 at line 91: `Cannot convert the
"System.Object[]" value of type "System.Object[]" to type "System.DateTime"`. Cause: Windows
PowerShell 5.1's `ConvertFrom-Json` writes a JSON array as one object, so line 90's
`@(... | ConvertFrom-Json)` holds one `Object[]` and `$_.createdAt` is an array. Assigning the
result to a variable first, then wrapping it in `@()`, enumerates it in both editions. A lane that
sees the failure may dispatch again and spend the task's cap of two.

## Acceptance criteria

- [ ] Run-finding works on `gh run list` JSON holding two or more runs under both `powershell` (5.1)
      and `pwsh`, shown by a check recorded in Notes that dispatches nothing.
- [ ] The script still prints `run:<id> <workflow> <ref> <url>` and keeps its exit codes.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
