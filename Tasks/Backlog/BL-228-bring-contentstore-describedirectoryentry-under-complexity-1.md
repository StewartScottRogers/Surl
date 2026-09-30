---
id: BL-228
title: Bring ContentStore.DescribeDirectoryEntry under complexity 10
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Surl.Content.UnitLibrary, Surl.Content.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-228 — Bring ContentStore.DescribeDirectoryEntry under complexity 10

## Goal

`Measure-CodeQuality.ps1` reports no failing member in `Surl.Content.UnitLibrary`.

## Context

- Found by BL-158 on 2026-09-29: `ContentStore.DescribeDirectoryEntry(string, string, string)`
  (`Surl.Content.UnitLibrary\ContentStore.cs`, about line 497) measures cyclomatic complexity 12
  from coverage (CRAP 12), above the root `CLAUDE.md` gate of 10. Line and branch coverage are
  100%. It last changed in BL-092 (`1d9c95b`).
- Split the method (extract a private method) without changing behaviour; the existing tests in
  `Surl.Content.UnitTests` must stay green unchanged.

## Acceptance criteria

- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Surl.Content.UnitLibrary`
      reports 0 failing members, 100% line and branch coverage.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-09-29: Created.
