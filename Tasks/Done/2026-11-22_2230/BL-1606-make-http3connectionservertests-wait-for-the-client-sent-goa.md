---
id: BL-1606
title: Make Http3ConnectionServerTests wait for the client-sent-GOAWAY note instead of reading the log at once
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-1606 — Make Http3ConnectionServerTests wait for the client-sent-GOAWAY note instead of reading the log at once

## Goal

`Http3ConnectionServerTests.ServeAsync_GoAwayMaxPushIdAndUnknownFramesOnTheControlStream_AreReadAndIgnored`, and every
other test in `Surl.Protocol.Http.UnitTests` that asserts a log note right after a response, passes every run on
every platform.

## Context

- PR #137 (shift 20261122-132012), CI run 45073890391, ubuntu-latest: `CollectionAssert.Contains(client.Log.Notes,
  "The client sent GOAWAY.")` failed at
  `Protocols/Http/Surl.Protocol.Http.UnitTests/Http3ConnectionServerTests.Errors.cs:65` (test at `:52-67`). The log
  held only the request line. The test waits for request stream 0 to end, but the control stream (where GOAWAY
  travels) is read on its own, so its note can come after the response. The failed job was rerun.
- Same family as BL-1528 (WebTransport session-accepted note) and BL-1375: a note read once, before it was written.

## Acceptance criteria

- [x] The test waits, bounded (with `client.UntilAsync` or the suite's own polling, no `Thread.Sleep`), until the
      GOAWAY note appears before asserting on it, and fails with the notes seen if it never does.
- [x] Every other test in `Surl.Protocol.Http.UnitTests` that asserts a note written by a different stream or task
      than the one it waited for uses the same wait, through one shared helper; the Log lists them.
- [x] The changed tests pass 20 runs in a row locally.
- [x] `dotnet build` clean and the full fast suite green.

## Notes

- Added Http3TestClient.UntilNoteAsync (bounded, lists notes seen on failure); used in 7 tests: Errors (GOAWAY, initial_max_streams_uni), Logins (2 VersionFallback), RequestStreams (head timeout, H3_INTERNAL_ERROR), ConnectionServerTests (shutdown GOAWAY).
- Verified: build clean, 20 runs green, fast Http suite 814 green.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing.
- 2026-11-22: Doing -> Done. Added Http3TestClient.UntilNoteAsync and used it in 7 tests; 20 runs green
