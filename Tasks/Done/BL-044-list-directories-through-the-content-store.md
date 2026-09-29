---
id: BL-044
title: List directories through the content store
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-010]
touches: [Surl.Content.UnitLibrary, Surl.Content.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-044 — List directories through the content store

## Goal

For a directory location the content store has mapped, a protocol server can get its
entries (name, file or directory, size, last modification time) in a stable order,
through the file-system seam and its `System.IO` implementation. Entries that would
escape the root, or that the hardening ADR hides, are left out.

## Context

- ADR-0002, decision 2: the content store "lists directories". Gopher menus (BL-034)
  need it first. HTTP directory listings, FTP `LIST` and `NLST`, and SFTP `READDIR`
  follow later.
- Extend the file-system seam from BL-008 and BL-009 with an enumeration member, the
  in-memory fake in `Surl.Content.UnitTests`, and BL-010's `System.IO` implementation,
  with its `[ExcludeFromCodeCoverage]` justification and an `Integration` test in a
  temporary directory.
- Order: ordinal by name, so output is identical on Windows, Linux and macOS. State it
  in the XML doc.
- Hidden entries: follow the hardening ADR (BL-024) on dot-files and symbolic links if it
  exists when this task runs. If not, list symbolic links only when their final target
  is inside the root, list dot-files, and file a follow-up to align with BL-024.
- Protocol-neutral: no menu or listing format here. Each protocol formats its own.

## Acceptance criteria

- [x] The content store returns the entries of a mapped directory with name, kind, size
      (files) and last modification time (UTC), ordinal by name. Asking for a file or a
      missing path gives a result saying so, not an exception.
- [x] Fast tests with the in-memory fake cover an empty directory, mixed files and
      subdirectories, ordering with mixed case and non-ASCII names, a symbolic link out
      of the root left out, and cancellation.
- [x] A `[TestCategory("Integration")]` test lists a temporary directory through the
      `System.IO` implementation.
- [x] `dotnet build Surl.Content.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Content.UnitLibrary`.

## Notes

- Shape: `IContentFileSystem.EnumerateDirectoryEntryNames(path)` (names only, any order);
  `ContentStore.ListDirectory(mapping, cancellationToken)` returns a
  `ContentDirectoryListing` (`IsListed`, `LocationKind`, `Entries`) of
  `ContentDirectoryEntry(Name, Kind, Length?, LastModifiedUtc)`. Synchronous, like the
  rest of the store's looks, with the token checked before the read and before every
  entry. `IContentFileSystem.GetLastWriteTimeUtc` now covers directories too (the disk
  implementation already answered for them).
- Decisions recorded in ADR-0008 (Decided by Claude under Stewart's delegation):
  ordinal order; names `MapRequestPath` would refuse are left out so every listed name
  can be requested; links out of the root and dangling links left out; in-root links
  listed under their own name with the target's status; Windows hidden/system
  attributes not skipped.
- Hidden entries: the hardening ADR (ADR-0006, from BL-024) exists and hides dot-files
  and unfollowed links by default, but as `ContentStore` options that BL-047 adds and
  that already depends on this task. So this listing keeps dot-files and in-root links,
  and BL-047 filters them; no new follow-up task was needed.
- `touches` widened to `Documentation/Planning/Decisions` for ADR-0008 and the index row;
  no task in Doing names it (BL-018: Surl.Protocol.Http; BL-029: Record-CurlExchange.ps1).
- Tests: 14 fast listing tests in `ContentStoreTests` (one test class per production
  class), 3 Integration tests in `DiskContentFileSystemTests` (the symbolic-link one runs
  on Linux and macOS only, like the existing link tests). Surl.Content.UnitTests: 137
  passed, 7 skipped (Windows-excluded link tests).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. ContentStore.ListDirectory lists a mapped directory's entries (name, kind, length, UTC time) in ordinal order through the seam and System.IO, leaving out links out of the root and unrequestable names
