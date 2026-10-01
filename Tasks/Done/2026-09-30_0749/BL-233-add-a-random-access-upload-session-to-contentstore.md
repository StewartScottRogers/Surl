---
id: BL-233
title: Add a random-access upload session to ContentStore
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-155, BL-232]
touches: [Surl.Content.UnitLibrary, Surl.Content.UnitTests]
requirement: FR-042
created: 2026-09-29
completed: 2026-09-30
---
# BL-233 — Add a random-access upload session to ContentStore

## Goal

`ContentStore.OpenUpload` opens a temporary-file upload that can be written and read at any
offset, resized, committed over its target or discarded, as ADR-0054 decision 14 item 4 specifies,
so the SFTP server's `OPEN`/`WRITE`/`READ`/`SETSTAT SIZE` (BL-166) can be built in
`Surl.Protocol.Ssh` alone.

## Context

- ADR-0054 decision 14 item 4 is the specification; decisions 9 and 10 are what uses it.
  ADR-0006 section 5 (a partial upload is deleted) and ADR-0015 (exposure) apply.
- `OpenUpload(ContentPathMapping, ContentUploadOpening)` answers `Opened`, `NotPermitted`,
  `NoSuchDirectory`, `IsADirectory`, `Exists` or `Absent`; `ContentUploadOpening` says whether to
  start from a copy of the existing bytes or empty, whether to create a missing file, and whether
  an existing one is refused.
- `ContentUploadSession`: `Length`, `WriteAtAsync(offset, bytes)` (zero-filling a gap),
  `ReadAtAsync(offset, buffer)`, `SetLengthAsync(length)`, `CommitAsync()` (the temporary file
  renamed over the target, answering `Written` or the failure) and `DisposeAsync()` (discarding an
  uncommitted upload). Any write or `SetLengthAsync` past `MaxUploadBytes` ends the session as
  `TooLarge` and deletes the temporary file.
- The seam gains `IContentFileSystem.OpenFileForAsyncReadWrite(string)` (default throwing, ADR-0015
  decision 7's pattern) returning a seekable, readable, writable stream; the in-memory file system's
  write stream gains seeking and `SetLength`, still within `MaxTotalBytes`.
- Depends on BL-232 only because both touch `Surl.Content`; they share no member.

## Acceptance criteria

- [x] `ContentStore.OpenUpload` answers each `ContentUploadOpening` outcome named in ADR-0054 decision 14 item 4, with tests on disk and in memory.
- [x] A session writes and reads at offsets (a gap zero-filled), sets its length, commits over the target, and discards on dispose without a commit, with tests.
- [x] Growth past `MaxUploadBytes` answers `TooLarge`, deletes the temporary file and leaves the target untouched, with a test.
- [x] `dotnet build` is clean and the fast tests pass; `Surl.Content.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Filed by BL-155 (ADR-0054 decision 14). BL-166 depends on it.
- The member is `OpenUploadAsync(mapping, opening, cancellationToken)`, not `OpenUpload`: it copies
  the target's bytes when the opening starts from them, so it is async like the other uploads, and
  answers a `ContentUploadOpeningOutcome` (result plus session).
- `CommitAsync` answers `NotPermitted` (deleting the temporary file) when a directory appeared at the
  target or its directory went away after opening, the same test `WriteUploadAsync` makes up front.
- A write or resize that throws (cancellation, a full in-memory file system) discards the upload
  and ends the session; later calls throw `InvalidOperationException`. After `TooLarge`, writes,
  resizes and commits answer `TooLarge` again, and reads throw.
- The catch blocks dispose synchronously: an `await` in a `catch` compiles to a rethrow branch no
  exception can take, which would cost the 100% branch coverage gate.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. OpenUploadAsync and ContentUploadSession land with tests on disk and in memory; build clean, fast tests green, Surl.Content.UnitLibrary at 100% line and branch.
