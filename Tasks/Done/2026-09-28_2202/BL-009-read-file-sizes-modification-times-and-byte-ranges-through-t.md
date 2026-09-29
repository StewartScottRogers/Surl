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
completed: 2026-09-28
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

- [x] The content store reports file, directory or nothing for a mapped location.
- [x] For a file it reports the length in bytes and the last modification time as a UTC
      `DateTimeOffset`.
- [x] It reads the whole file, or the inclusive range first..last, as an async stream or
      copy-to-destination. The `/feature` plan picks one and the XML doc states it.
- [x] A range whose first offset is at or past the length returns an "unsatisfiable"
      result carrying the length. A last offset past the end is clamped to length − 1.
      A first offset greater than the last is rejected. Each is pinned by a fast test.
- [x] A zero-length file reads as zero bytes, pinned by a test.
- [x] Cancellation before and during a read is covered by tests, using
      `CancellationToken.ThrowIfCancellationRequested()` so the exact exception type is
      `OperationCanceledException` (`.claude/rules/testing.md`).
- [x] `dotnet build Surl.Content.UnitLibrary -warnaserror` is clean, the fast tests are
      green with no `Integration` test in `Surl.Content.UnitTests`, and
      `Measure-CodeQuality.ps1` reports no failing member in `Surl.Content.UnitLibrary`.

## Notes

- Plan (decided in-session, no wire bytes involved, so no upstream curl measurement):
  - Seam `IContentFileSystem` gains `GetFileLength`, `GetLastWriteTimeUtc` and
    `OpenFileForAsyncRead` (a readable, seekable stream). No production implementation
    of the seam exists yet, so nothing else had to change; the in-memory fake in
    `Surl.Content.UnitTests` holds file bytes and a write time per file.
  - `ContentStore.GetEntryKind(mapping)` asks the seam now (the mapping's own
    `EntryKind` is a snapshot from mapping time). `GetFileStatus(mapping)` returns a
    `ContentFileStatus(Length, LastModifiedUtc)`, or null for a directory or nothing; the
    time is normalised with `ToUniversalTime()` so the offset is always zero.
  - Ranges are a separate value, `ContentByteRange`: `WholeFile(length)` or
    `Select(length, first, last)`. First at or past the length gives an unsatisfiable
    range carrying the length; last past the end is clamped to length - 1; first > last
    or a negative offset throws `ArgumentOutOfRangeException`. Selecting the range apart
    from the read lets a protocol server announce the length (HTTP `Content-Length` /
    `Content-Range`, FTP `150 (n bytes)`) before any byte is written.
  - Read shape: copy-to-destination, `CopyFileBytesAsync(mapping, range, destination, ct)`,
    returning the bytes copied. Chosen over returning a stream so the file is opened and
    disposed inside the store and a caller cannot leak a handle. If the file shrank since
    the range was selected the copy stops at its end and returns the smaller count.
  - Cancellation: `ThrowIfCancellationRequested()` before the file is opened and before
    every read, so the exception is exactly `OperationCanceledException`.
- Verified: 105 fast tests green in `Surl.Content.UnitTests` (none `Integration`);
  `Measure-CodeQuality.ps1 -Library Surl.Content.UnitLibrary` reports 100% line, 100%
  branch, worst CRAP 8, 0 failing members; `dotnet build Surl.Content.UnitLibrary
  -warnaserror` clean; whole-solution build and fast tests green.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. The content store reports file/directory/nothing, file length and UTC mtime, and copies the whole file or a validated inclusive byte range through the seam
