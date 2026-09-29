---
id: BL-044
title: List directories through the content store
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-010]
touches: [Surl.Content.UnitLibrary, Surl.Content.UnitTests]
requirement: none
created: 2026-09-28
completed:
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

- [ ] The content store returns the entries of a mapped directory with name, kind, size
      (files) and last modification time (UTC), ordinal by name. Asking for a file or a
      missing path gives a result saying so, not an exception.
- [ ] Fast tests with the in-memory fake cover an empty directory, mixed files and
      subdirectories, ordering with mixed case and non-ASCII names, a symbolic link out
      of the root left out, and cancellation.
- [ ] A `[TestCategory("Integration")]` test lists a temporary directory through the
      `System.IO` implementation.
- [ ] `dotnet build Surl.Content.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Content.UnitLibrary`.

## Notes

## Log

- 2026-09-28: Created.
