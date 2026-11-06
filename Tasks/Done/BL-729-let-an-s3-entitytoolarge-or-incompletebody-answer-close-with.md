---
id: BL-729
title: Let an S3 EntityTooLarge or IncompleteBody answer close without draining the body
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-419]
touches: [Protocols/Http/Surl.HttpServing.UnitLibrary, Protocols/Http/Surl.HttpServing.UnitTests, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitLibrary, Protocols/FileTransfer/Surl.Extended.Protocol.S3.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-729 — Let an S3 EntityTooLarge or IncompleteBody answer close without draining the body

## Goal

An S3 `400 EntityTooLarge` or `400 IncompleteBody` answer carries its XML `Error` body and closes the connection without draining the body, as ADR-0108 decision 7 says.

## Context

- `Http1AnswerWriter.WriteRefusalAsync` writes a refusal's head only, so BL-419 sends these as ordinary error answers: `EntityTooLarge` leaves the body unread, which closes after the lingering drain (ADR-0024); `IncompleteBody` after a body read in part leaves the connection open.
- Needs `Surl.HttpServing` to write a refusal's body (or an answer flag meaning close after this answer) without changing the HTTP server's recorded bytes. Related: BL-720.

## Acceptance criteria

- [x] A named S3 test shows `EntityTooLarge` for an announced body past `--max-filesize` answered with the XML body and `Connection: close`, the body not read.
- [x] A named S3 test shows `IncompleteBody` answered with the XML body and `Connection: close`.
- [x] Every existing HTTP test passes unchanged; `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch coverage.

## Notes

- What the Context described had partly changed: BL-720 already made `Http1AnswerWriter.WriteRefusalAsync` write a refusal's `HttpAnswerBody.Bytes` body, so what was missing was a way to close without ADR-0024's lingering drain.
- Decision (Claude under Stewart's delegation; to be written into ADR-0108 as Amendment 4 by BL-933):
  1. `HttpAnswer.DrainsBeforeClose` (new optional positional parameter, default `true`) says whether a closing answer is followed by the lingering drain. `Http1AnswerWriter.DrainBeforeCloseAsync` honours it after a refusal and after any other answer that closes. Why a flag rather than a new answer kind: every existing server keeps its bytes and drain by default, and the writer stays the only place that decides how a connection ends.
  2. `S3Error.ClosesWithoutDrain` marks `EntityTooLarge` and `IncompleteBody`; `S3Reply.Error` turns either into `IsRefusal: true, DrainsBeforeClose: false`, so wherever PutObject, UploadPart or CompleteMultipartUpload answer them they carry the XML `Error` body, `Connection: close` within the one-second refusal write deadline, and leave the rest of the body unread. Why: ADR-0108 decision 7 says "then close (the body is not drained)"; it also closes an UploadPart whose staged bytes ran past the limit after its body was read whole, which until now kept the connection open.
  3. The close is left to the transport: on TCP, ADR-0021's lingering close still runs after the exchange, so the 400 is not lost to a reset. On a transport without it a client still sending may see a reset - the stated cost of not reading a body already refused.
- ADR-0108 could not be edited here: BL-701 (in Doing) holds `Documentation/Planning/Decisions`. Filed BL-933 (docs, depends on BL-729) to write Amendment 4 and correct Amendment 2 point 4. The decision is also recorded in both libraries' `CLAUDE.md`.
- Tests: `S3PutObjectTests.ServeAsync_KeepAlivePutObjectAnnouncedPastMaxFilesize_Is400EntityTooLargeClosedWithTheBodyUnread` (keep-alive request; the exact XML answer with `Connection: close`, and the body chunk still unread on the connection afterwards), `S3PutObjectTests.ServeAsync_KeepAlivePutObjectWhoseBodyEndsEarly_Is400IncompleteBodyAndCloses`; `Http1AnswerWriterTests` gained three (refusal without drain, refusal with drain, closing non-refusal without drain).
- Coverage: S3 library 100% line and branch from its own tests; every line and branch added to `Surl.HttpServing` (`HttpAnswer`, `Http1AnswerWriter.WriteRefusalAsync`, `DrainBeforeCloseAsync`) is covered by `Surl.HttpServing.UnitTests`. That project alone leaves older paths (file copy failures, takeover) to the HTTP server's tests, as it did before this task.
- Build `-warnaserror` clean; fast tests 79 projects, 21336 passed, 0 failed; `dotnet format --verify-no-changes` clean on all four projects.

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. S3 EntityTooLarge and IncompleteBody are refusals with the XML body and Connection: close that leave the rest of the body undrained (HttpAnswer.DrainsBeforeClose)
