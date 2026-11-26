---
id: BL-1792
title: Make dispatch-workflow.ps1 exit with its documented refusal and failure codes
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [.claude/skills/task-board/dispatch-workflow.ps1]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-1792 — Make dispatch-workflow.ps1 exit with its documented refusal and failure codes

## Goal

`dispatch-workflow.ps1` exits 2, 3, 4, 5 or 6 for the refusal or failure that line names,
not 1.

## Context

Found during BL-1791 on 2026-11-26. The script sets `$ErrorActionPreference = 'Stop'`, so each
`Write-Error ...; exit N` throws at `Write-Error` and `exit N` never runs. Measured:
`-Ref master -DryRun` exits 1 under both `powershell -File` (5.1) and `pwsh -File`, where the
script intends 2. Writing the message to standard error without throwing fixes it.

## Acceptance criteria

- [x] `-Id BL-1792 -Workflow ci -Ref master -DryRun` prints the refusal to standard error and exits
      2 under both `powershell` and `pwsh`.
- [x] The other `Write-Error ...; exit N` lines exit with their N, shown by a check recorded in
      Notes that dispatches nothing.

## Notes

- 2026-11-26: Each `Write-Error ...; exit N` now calls `Write-FailureAndExit`, which writes one line
  with `[Console]::Error.WriteLine` and exits N. Chosen over `Write-Error -ErrorAction Continue`,
  which adds PowerShell's multi-line error record around the message. The exit codes are listed in
  the script's `.DESCRIPTION`.
- 2026-11-26: Check, run under `powershell` and `pwsh` with identical results. In the lane
  checkout, `-Ref master -DryRun`: exit 2, refusal on standard error. In a temporary repository
  whose `origin` was a local bare repository holding `factory/pushed`, with a fake `gh.cmd` first
  on PATH (confirmed with `Get-Command gh` before any run), so nothing could reach GitHub:
  - `-Ref master -DryRun`: exit 2.
  - `-Ref factory/pushed -MaxPerTask 0 -DryRun`: exit 3.
  - `-Ref factory/absent -DryRun`: exit 4.
  - `-Ref factory/pushed`, fake `gh` exiting 1: exit 5.
  - `-Ref factory/pushed`, fake `gh run list` answering `[]`: exit 6.
  - `-Ref factory/pushed -DryRun`: exit 0, the `dry-run:` line on standard output.
  Every failing case wrote its message to standard error and nothing to standard output. The
  temporary directory was deleted afterwards.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. dispatch-workflow.ps1 exits 2 to 6 for each refusal or failure, with the message on standard error, under powershell and pwsh
