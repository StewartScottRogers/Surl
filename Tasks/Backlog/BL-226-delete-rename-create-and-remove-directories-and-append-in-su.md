---
id: BL-226
title: Delete, rename, create and remove directories and append in Surl.Content's ContentStore
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Content.UnitLibrary, Surl.Content.UnitTests]
requirement: FR-036
created: 2026-09-29
completed:
---
# BL-226 — Delete, rename, create and remove directories and append in Surl.Content's ContentStore

## Goal

`ContentStore` can delete a file, rename a file or directory (replacing an existing file),
create and remove an empty directory, and append an upload to an existing file, each under the
same path mapping, exposure and `--max-filesize` rules as `WriteUploadAsync`, so the FTP server
(BL-180) can answer `DELE`, `RNFR`/`RNTO`, `MKD`, `RMD` and `APPE`.

## Context

- Decision: ADR-0052 decision 8 (the replies each outcome maps to) and its Consequences.
- Code: `Surl.Content.UnitLibrary/ContentStore.cs` (`WriteUploadAsync`, its temporary dot-file
  and rename), `IContentFileSystem.cs` (already has `DeleteFile`, `MoveFileReplacing`,
  `CreateDirectory` with read-only defaults; lacks directory removal and append),
  `DiskContentFileSystem.cs`, `InMemoryContentFileSystem.cs` (its 256 MiB bound, ADR-0031).
- Rules: ADR-0006 section 2 (dot-files, links), ADR-0031 decision 5 (nothing under `/.surl`),
  ADR-0006 section 5 (an append past `--max-filesize` counts the existing length and deletes
  nothing that was there before).
- Adding a member to `IContentFileSystem` with a default body keeps its test fakes compiling
  (ADR-0015).

## Acceptance criteria

- [ ] `ContentStore` has members to delete a file, rename an entry, create a directory, remove an
      empty directory and append an upload, each returning a result the FTP server can map to
      ADR-0052 decision 8's replies (done, absent, not empty, exists, too large, not permitted).
- [ ] Each refuses a path under `/.surl`, a hidden dot-file and a link outside the root as
      `ContentStore` refuses them for reads, and works on both `DiskContentFileSystem` and
      `InMemoryContentFileSystem`.
- [ ] An append past `--max-filesize` leaves the original file as it was.
- [ ] `Surl.Content.UnitLibrary` keeps 100% line and branch coverage, complexity at most 10.

## Notes

## Log

- 2026-09-29: Created.
