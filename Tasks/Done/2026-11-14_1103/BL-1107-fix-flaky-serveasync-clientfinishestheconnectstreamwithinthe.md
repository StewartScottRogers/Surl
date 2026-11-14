---
id: BL-1107
title: Fix flaky ServeAsync_ClientFinishesTheConnectStreamWithinTheShutdownGrace_ClosesTheConnectionAtOnce
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-1107 — Fix flaky ServeAsync_ClientFinishesTheConnectStreamWithinTheShutdownGrace_ClosesTheConnectionAtOnce

## Goal

`ServeAsync_ClientFinishesTheConnectStreamWithinTheShutdownGrace_ClosesTheConnectionAtOnce` passes every time on Linux and macOS CI.

## Context

Flaky on CI. Found by the repeated-legs run https://github.com/StewartScottRogers/Surl/actions/runs/43436178802 (`gh workflow run CI --ref factory/phase-1 -f attempts=5`, BL-1104/BL-1105). It failed once, on Ubuntu attempt 2. Message: `Assertion failed. Expected strings to be equal. Strings have different lengths (expected: 10, actual: 0) ... The grace never passed, so no shutdown WT_CLOSE_SESSION. expected: "800078AE00" actual: ""`.

Test: `Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests/WebTransportSessionTests.cs`; library `Surl.Extended.Protocol.WebTransport.UnitLibrary`. Time must come from the injected `TimeProvider`, so look for a race between the fake clock advancing and the session reading the client's finish.

## Acceptance criteria

- [x] The cause is stated under Notes.
- [x] The test (or the library) is changed so the race is gone, with no real-time sleep.
- [x] The test passes 200 times in a row locally (`dotnet test --filter` repeated) or in a CI repeat run, stated in the Log.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

Filed by BL-1105 on 2026-11-13.

Cause: WebTransportSession registered its shutdown drain write (AddShutdownWrite) in ServeAsync, after the 200 had been written. A client that saw the 200 and cancelled the exchange at once could start the shutdown before the registration, so no WT_DRAIN_SESSION went out (expected 800078AE00, got empty).

Fix (library, no sleep): the session registers the write in its constructor, before the 200 is written; the write waits on an `answered` completion source that the handler sets (MarkAnswered) once the 200 is written, so the drain can never precede the 200. Files: WebTransportSession.cs, WebTransportConnectHandler.cs.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing.
- 2026-11-13: Test passed 200 runs in a row locally (dotnet test --no-build --filter, 0 failures); build clean, fast tests green.
- 2026-11-13: Doing -> Done. WebTransport session registers its shutdown drain before the 200, so the grace test no longer races
