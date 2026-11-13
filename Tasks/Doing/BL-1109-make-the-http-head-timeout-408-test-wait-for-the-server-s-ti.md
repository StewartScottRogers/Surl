---
id: BL-1109
title: Make the HTTP head-timeout 408 test wait for the server's timer before advancing the fake clock
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-13
completed:
---
# BL-1109 — Make the HTTP head-timeout 408 test wait for the server's timer before advancing the fake clock

## Goal

`HeadTimeoutTests.PartialHead_AfterHeadTimeout_Answers408AndCloses` passes on CI's Windows,
Linux and macOS jobs every run, and a partial request head still gets the recorded 408 reply
once the head timeout expires.

## Context

CI run 43506096520 (PR #33, frozen branch `factory/merge-20261113-123056`, commit e6077e2):
the test failed only on "Build and test (ubuntu-latest)", at line 26,
`CollectionAssert.AreEqual(RecordedResponse("head-timeout-408"), connection.WrittenBytes)`:
"Different number of elements". It passed on Windows and macOS. That holds the merge of
#33 to `master`.

- Test: `Protocols/Http/Surl.Protocol.Http.UnitTests/HeadTimeoutTests.cs:14`. It starts
  `ServeAsync` and then calls `clock.Advance(HeadTimeout - 1 s)` on the `ManualTimeProvider`
  straight away. If the server has not yet created its head-timeout timer by then, the timer
  starts later, measured from the advanced time, so the expiry the test expects never
  arrives where the test expects it.
- Production: the head-timeout path in `Protocols/Http/Surl.Protocol.Http.UnitLibrary`.
- Find out first which side is wrong: a test that advances before the timer exists, or a
  server that measures the head timeout from the wrong moment.

## Acceptance criteria

- [ ] The task's Log names the cause: either the test advances the clock too early, or production starts the head timeout from the wrong time.
- [ ] The test advances the fake clock only after the server's head-timeout timer exists, by waiting on the fake (e.g. the `ManualTimeProvider` exposing its pending timers, or the connection having been read), never with `Task.Delay`, `Thread.Sleep` or real time.
- [ ] The other `HeadTimeoutTests` that advance the clock straight after `ServeAsync` get the same wait.
- [ ] The test passes 50 runs in a row on Windows: `dotnet test Protocols/Http/Surl.Protocol.Http.UnitTests --filter "FullyQualifiedName~HeadTimeoutTests"` looped 50 times.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green. Surl.Protocol.Http.UnitLibrary keeps 100% line and branch coverage.

## Notes

Filed by an interactive session on 2026-11-13 after PR #33's Linux job failed. Lanes test only on
Windows, where this passes, so reason about the race rather than rely on reproducing it.
BL-1110 is the same kind of race in the WebSocket tests.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing.
