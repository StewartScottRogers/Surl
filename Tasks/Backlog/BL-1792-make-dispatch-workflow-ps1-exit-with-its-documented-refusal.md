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
completed:
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

- [ ] `-Id BL-1792 -Workflow ci -Ref master -DryRun` prints the refusal to standard error and exits
      2 under both `powershell` and `pwsh`.
- [ ] The other `Write-Error ...; exit N` lines exit with their N, shown by a check recorded in
      Notes that dispatches nothing.

## Notes

## Log

- 2026-11-26: Created.
