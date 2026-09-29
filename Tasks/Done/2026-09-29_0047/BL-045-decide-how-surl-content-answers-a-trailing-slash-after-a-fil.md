---
id: BL-045
title: Decide how Surl.Content answers a trailing slash after a file and a served root that is not fully qualified
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-008]
touches: [Surl.Content.UnitLibrary, Surl.Content.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
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

- [x] A test in `Surl.Content.UnitTests` pins what `MapRequestPath("/file.txt/")` returns
      when `file.txt` is a file, and the `ContentStore` XML doc states it.
- [x] A test pins what the `ContentStore` constructor does with a served root that is not
      fully qualified, and the XML doc states it.
- [x] `dotnet test --filter "TestCategory!=Integration"` is green and
      `Measure-CodeQuality.ps1` reports no failing member in `Surl.Content.UnitLibrary`.

## Notes

- Measured: pinned upstream curl 8.21.0 (win-x64 reference) sends `GET /file.txt/ HTTP/1.1`
  as written (`Record-CurlExchange.ps1`), so the answer is Surl's to decide. Decided in
  ADR-0018 (Claude under Stewart's delegation).
- Trailing slash: a request path ending in `/` names a directory. A file there maps with
  `EntryKind` `None`, and the mapping's internal `NamesADirectory` keeps every later look
  (`GetEntryKind`, `GetFileStatus`, `ListDirectory`, `CopyFileBytesAsync`) answering it as
  nothing; an upload to any path ending in `/` is `NotPermitted`. Pinned in
  `ContentStoreTests.TrailingSlashAndRoot.cs`.
- Served root: the constructor throws `ArgumentException` for a root relative to the
  current directory (`srv`, `C:`, `C:srv`); a fully qualified root or one starting with a
  separator is kept as given. `/srv/www` is accepted on Windows (rooted on the current
  drive) because every test spells its root that way; requiring `IsPathFullyQualified`
  would have broken them on Windows. Platform answers pinned with `OSCondition`.
- `touches` gained `Documentation/Planning/Decisions` for ADR-0018; no task in Doing names it.
- The existing upload test for a missing file-system root now stops at the trailing `/`, so
  `WriteUploadAsync_LinkResolvingToAMissingFileSystemRoot_IsNotPermitted` keeps the empty
  parent branch of `ParentDirectoryOf` covered.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ContentStore answers /file.txt/ as a missing path and refuses a served root relative to the current directory (ADR-0018)
