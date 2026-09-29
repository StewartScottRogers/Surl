---
id: BL-045
title: Decide how Surl.Content answers a trailing slash after a file and a served root that is not fully qualified
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-008]
touches: [Surl.Content.UnitLibrary, Surl.Content.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-045 — Decide how Surl.Content answers a trailing slash after a file and a served root that is not fully qualified

## Goal

`ContentStore` gives a decided, tested answer for `/file.txt/` (a trailing slash after a
file) and for a served root that is not a fully qualified path.

## Context

- Found by the code review of BL-008. Neither is an escape; both are behaviour gaps.
- Today `ContentStore.MapRequestPath("/file.txt/")` maps to the file with
  `ContentEntryKind.File`, where a POSIX file system refuses such a path (`ENOTDIR`).
  Measure what upstream curl expects from a server for this case with
  `Record-CurlExchange.ps1` before pinning an answer.
- The `ContentStore` constructor documents that the served root is a full path but does
  not check it, so a root like `srv` or `C:` would make `Location` depend on the current
  directory. `Path.IsPathFullyQualified("/srv")` is false on Windows, so a check needs
  platform-neutral tests (see root `CLAUDE.md`, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] A test in `Surl.Content.UnitTests` pins what `MapRequestPath("/file.txt/")` returns
      when `file.txt` is a file, and the `ContentStore` XML doc states it.
- [ ] A test pins what the `ContentStore` constructor does with a served root that is not
      fully qualified, and the XML doc states it.
- [ ] `dotnet test --filter "TestCategory!=Integration"` is green and
      `Measure-CodeQuality.ps1` reports no failing member in `Surl.Content.UnitLibrary`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
