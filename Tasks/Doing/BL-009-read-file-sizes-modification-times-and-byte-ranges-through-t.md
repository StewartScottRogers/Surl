---
id: BL-009
title: Read file sizes, modification times and byte ranges through the content store
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-008]
touches: [Surl.Content.UnitLibrary, Surl.Content.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-009 — Read file sizes, modification times and byte ranges through the content store

## Goal

For a location the content store has mapped (BL-008), a protocol server can ask whether
it is a file, a directory or nothing. For a file it can read its size and last
modification time, and read all of its bytes or a validated byte range, all through the
file-system seam.

## Context

- ADR-0002, decision 2 lists these duties of `Surl.Content`: read byte ranges, report
  sizes and modification times.
- Builds on the content-store type and file-system seam from BL-008. Extend the seam
  with the members this needs (length, last write time in UTC, open for asynchronous
  read), and extend the in-memory fake in `Surl.Content.UnitTests` to match.
- Protocol-neutral: no HTTP `Range` header parsing here. The caller passes an inclusive
  first and last byte offset, or asks for the whole file. HTTP, FTP `REST`, TFTP and
  SFTP all use it.
- Async all the way (root `CLAUDE.md`). Honour a `CancellationToken`.

## Acceptance criteria

- [ ] The content store reports file, directory or nothing for a mapped location.
- [ ] For a file it reports the length in bytes and the last modification time as a UTC
      `DateTimeOffset`.
- [ ] It reads the whole file, or the inclusive range first..last, as an async stream or
      copy-to-destination. The `/feature` plan picks one and the XML doc states it.
- [ ] A range whose first offset is at or past the length returns an "unsatisfiable"
      result carrying the length. A last offset past the end is clamped to length − 1.
      A first offset greater than the last is rejected. Each is pinned by a fast test.
- [ ] A zero-length file reads as zero bytes, pinned by a test.
- [ ] Cancellation before and during a read is covered by tests, using
      `CancellationToken.ThrowIfCancellationRequested()` so the exact exception type is
      `OperationCanceledException` (`.claude/rules/testing.md`).
- [ ] `dotnet build Surl.Content.UnitLibrary -warnaserror` is clean, the fast tests are
      green with no `Integration` test in `Surl.Content.UnitTests`, and
      `Measure-CodeQuality.ps1` reports no failing member in `Surl.Content.UnitLibrary`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
