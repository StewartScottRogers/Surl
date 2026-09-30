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
completed: 2026-09-29
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

- [x] `ContentStore` has members to delete a file, rename an entry, create a directory, remove an
      empty directory and append an upload, each returning a result the FTP server can map to
      ADR-0052 decision 8's replies (done, absent, not empty, exists, too large, not permitted).
- [x] Each refuses a path under `/.surl`, a hidden dot-file and a link outside the root as
      `ContentStore` refuses them for reads, and works on both `DiskContentFileSystem` and
      `InMemoryContentFileSystem`.
- [x] An append past `--max-filesize` leaves the original file as it was.
- [x] `Surl.Content.UnitLibrary` keeps 100% line and branch coverage, complexity at most 10.

## Notes

- Delivered: `ContentStore.DeleteFile`, `RenameEntry`, `CreateDirectory`,
  `RemoveEmptyDirectory` (each returning the new `ContentChangeResult`: `Done`, `Absent`,
  `NotEmpty`, `Exists`, `NoSuchDirectory`, `NotPermitted`) and `AppendUploadAsync` (returning
  `ContentUploadResult`). The seam gained `MoveDirectory` and `RemoveEmptyDirectory` with
  read-only default bodies (ADR-0015), implemented by `DiskContentFileSystem` and
  `InMemoryContentFileSystem`.
- Choice: `NoSuchDirectory` is its own member rather than folded into `Absent`, because
  ADR-0052 decision 8 answers a missing parent (`MKD`, `RNTO`) differently from a missing
  entry. "Too large" stays on `ContentUploadResult`, the only operation that can exceed a size.
- Choice: `Exists` covers every entry in the way: `MKD` over a file or directory, `RNTO` onto
  a directory, or a directory renamed onto a file. The FTP server knows which command it is
  answering, so it can pick decision 8's text (`550 Already exists` or `553 Cannot rename onto
  a directory`).
- Choice: an append writes a temporary dot-file holding a copy of the existing bytes followed
  by the appended ones, then renames it over the target, as `WriteUploadAsync` does. So an
  append that goes too large, fails or is cancelled leaves the original file exactly as it
  was. The limit counts the existing length. A file already past the limit makes every
  append too large, and no byte of the upload is read.
- Choice: hidden or `/.surl` locations are `Absent` when they name an existing entry (delete,
  rename source, rmdir) and `NotPermitted` when they name a new name (mkdir, rename
  destination), per decision 8's last bullets. A link outside the root is a mapping refusal,
  so every member throws `ArgumentException` for it, as the reads do. A symbolic link that
  is followed resolves to its target, so delete and rename act on the target.
- Choice: the served root itself cannot be removed, renamed or replaced (`NotPermitted`). A
  directory cannot be renamed into itself (`NotPermitted`). Renaming onto the same location
  is `Done` and changes nothing. `RemoveEmptyDirectory` counts hidden entries, so it never
  removes a dot-file along with the directory.
- Also brought `ContentStore.DescribeDirectoryEntry` down from Cobertura complexity 12 to
  within 10 (split into `IsEntryListed` and `DescribeListedEntry`), so the whole library
  passes `Measure-CodeQuality.ps1`: 100% line, 100% branch, worst CRAP 10.
- Conformance stage: nothing to check against upstream curl here, because this library puts
  no bytes on the wire. BL-180 (FTP server) and BL-183 check curl's exchanges.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. ContentStore deletes, renames, creates and removes directories and appends uploads on disk and in memory, under the exposure, /.surl and --max-filesize rules
