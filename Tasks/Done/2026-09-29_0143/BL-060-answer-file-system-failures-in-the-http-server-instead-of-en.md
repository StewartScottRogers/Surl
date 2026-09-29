---
id: BL-060
title: Answer file-system failures in the HTTP server instead of ending the exchange
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-018]
touches: [Surl.Protocol.Http.UnitLibrary, Surl.Protocol.Http.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-060 — Answer file-system failures in the HTTP server instead of ending the exchange

## Goal

When the content store throws `IOException` or `UnauthorizedAccessException` while
`HttpProtocolServer` answers a GET or HEAD, the server answers in HTTP's own words when no
response byte has been sent yet, and aborts the connection with a log note when the head
has already gone out, instead of letting the exception escape `ServeAsync`.

## Context

- BL-018 built the server (`HttpRequestResponder`, ADR-0008). Today
  `ContentStore.GetFileStatus` throwing (a file without read permission) escapes with no
  response, and `OpenFileForAsyncRead` throwing after the 200 head (the file was deleted
  in between) escapes without a note; the engine then aborts (ADR-0004, section 5).
- ADR-0006, section 3: the reply is fixed text and never carries an exception message or a
  local path; the verbose log may. Decide the status for an unreadable file (404, which
  hides it, or 500) and record it in a new ADR that supersedes the affected ADR-0008 row.
- Test with the `InMemoryContentFileSystem` in `Surl.Protocol.Http.UnitTests`, extended to
  throw from `GetLastWriteTimeUtc`, `GetFileLength` or `OpenFileForAsyncRead`.

## Acceptance criteria

- [x] A fast test makes the file status throw and asserts the decided status, its exact
      bytes, and that the connection is kept or closed as decided.
- [x] A fast test makes the open throw after the head was sent and asserts the connection
      was aborted with a note naming the file.
- [x] If the decided answer is not 404, it was fed to pinned upstream curl 8.21.0 with
      `Record-CurlExchange.ps1 -Response` and the recording is committed.
- [x] `dotnet build` is clean, the fast tests are green, and `Measure-CodeQuality.ps1`
      reports no failing member in `Surl.Protocol.Http.UnitLibrary`.

## Notes

Filed by BL-018 from its code review (2026-09-28).

Delivered 2026-09-29 (dark factory lane 1):

- Decided in ADR-0023: an `IOException` or `UnauthorizedAccessException` from
  `GetFileStatus` is answered 404, byte for byte the missing-file 404, keeping the
  connection as persistence decides; the exception type and message go to the log only.
  404 over 500 because a 500 would confirm something exists at the path (ADR-0006,
  section 2). The 404 is already recorded against pinned upstream curl 8.21.0
  (`not-found-fail`), so no new recording was needed (third criterion: not applicable).
- The same failures from the open or read after the 200 head abort the connection with
  the note `<location> could not be read after the 200 head was sent (...)`. Any other
  exception still escapes as a defect.
- `ConnectionWriteStream.HasFailedWrite` tells a failed connection write apart from a
  file failure, so a peer reset during the copy is rethrown, not noted as the file's.
- Tests: `FileSystemFailureTests` (new), `InMemoryContentFileSystem.FailOn`, a
  `FailingWriteConnection` double; `ErrorTextTests` no longer tolerates the escaping
  exception. Http tests 296 green; `Measure-CodeQuality.ps1` reports 100% line and branch
  coverage and no failing member in `Surl.Protocol.Http.UnitLibrary`.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. A file the HTTP server cannot read is answered 404 before the head and aborts with a log note after it (ADR-0023)
