---
id: BL-232
title: Add entry status, last-write-time setting and non-replacing rename to ContentStore
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-155]
touches: [Surl.Content.UnitLibrary, Surl.Content.UnitTests]
requirement: FR-042
created: 2026-09-29
completed:
---
# BL-232 — Add entry status, last-write-time setting and non-replacing rename to ContentStore

## Goal

`ContentStore` answers the status of a file or a directory, sets an entry's last write time and
renames without replacing, as ADR-0054 decision 14 items 1 to 3 specify, so the SCP and SFTP
servers (BL-164 to BL-166) can be built in `Surl.Protocol.Ssh` alone.

## Context

- ADR-0054 decision 14 (`Documentation/Planning/Decisions/ADR-0054-how-the-ssh-server-answers-upstream-curls-scp-and-sftp-requests.md`)
  is the specification; ADR-0015 governs exposure (hidden entries, `/.surl`, `AllowUploads`).
- Item 1: `GetEntryStatus(ContentPathMapping)` - kind, a file's length, last write time, for a file
  or a directory; `null` for nothing or a hidden entry. `GetFileStatus` answers files only.
- Item 2: `SetLastWriteTime(ContentPathMapping, DateTimeOffset)` answering `ContentChangeResult`
  (`NotPermitted` with uploads off, `Absent` for nothing or a hidden entry); the seam gains
  `IContentFileSystem.SetLastWriteTimeUtc(string, DateTimeOffset)` as a default member throwing
  `NotSupportedException` (ADR-0015 decision 7's pattern), implemented by `DiskContentFileSystem`
  and `InMemoryContentFileSystem`.
- Item 3: `RenameEntryWithoutReplacing(ContentPathMapping, ContentPathMapping)` - `RenameEntry`'s
  rules, except any entry at the destination answers `ContentChangeResult.Exists`; the seam gains
  `MoveFileWithoutReplacing(string, string)` (default throwing), the disk using
  `File.Move(..., overwrite: false)`.
- BL-164 needs item 2, BL-165 item 1, BL-166 items 1 to 3.

## Acceptance criteria

- [ ] `ContentStore.GetEntryStatus` answers a file's and a directory's kind and last write time, and `null` for a missing or hidden entry, with `Surl.Content.UnitTests` covering each on disk and in memory.
- [ ] `ContentStore.SetLastWriteTime` sets a file's and a directory's time, answers `NotPermitted` without `AllowUploads` and `Absent` for a missing or hidden entry, with tests.
- [ ] `ContentStore.RenameEntryWithoutReplacing` renames a file and a directory and answers `Exists` when anything is at the destination, with tests.
- [ ] `dotnet build` is clean and the fast tests pass; `Surl.Content.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Filed by BL-155 (ADR-0054 decision 14). BL-164, BL-165 and BL-166 depend on it.

## Log

- 2026-09-29: Created.
