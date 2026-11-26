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
completed: 2026-11-26
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

- [x] Run-finding works on `gh run list` JSON holding two or more runs under both `powershell` (5.1)
      and `pwsh`, shown by a check recorded in Notes that dispatches nothing.
- [x] The script still prints `run:<id> <workflow> <ref> <url>` and keeps its exit codes.

## Notes

- Run-finding is now the function `Select-DispatchedRun -RunListJson -StartedUtc`.
- A second 5.1 defect sat behind the first: `[datetime]'...Z'` there gives local time, which was
  compared with the UTC start time. At UTC-7 every new run would have looked 7 hours old (exit 6
  path). `createdAt` is now converted with `.ToUniversalTime()`, a no-op under `pwsh`.
- Check on 2026-11-26, 5.1.26100 and pwsh 7.6.6, machine at UTC-7, nothing dispatched:
  1. Lifted `Select-DispatchedRun` out of the script by its syntax tree and fed it JSON: two new
     runs (newest chosen), three with one new, two old (none), one new hours after the start with
     one old, one 10 s before the start (chosen), `[]` (none), empty output (none). All 7 passed in
     both editions.
  2. Ran the whole script with `gh` and `git` replaced by global functions and the dispatch count
     file under `%TEMP%`: both editions printed
     `run:222 ci factory/x https://example.invalid/222` and exited 0.
  3. `-Ref factory/phase-1 -DryRun` printed its `dry-run:` line and exited 0 in both editions.
- Exit codes are unchanged. Refusals and failures actually exit 1, not the 2-6 written in the
  script, because `Write-Error` throws under `Stop`; that predates this task and is BL-1792.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. dispatch-workflow.ps1 finds the run it dispatched under Windows PowerShell 5.1 and pwsh, in any time zone.
