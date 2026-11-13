---
id: BL-1111
title: Name why the HTTP head-timeout 408 test fails on Linux and make it wait for the server's timer
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-13
completed:
---
# BL-1111 — Name why the HTTP head-timeout 408 test fails on Linux and make it wait for the server's timer

## Goal

`HeadTimeoutTests.PartialHead_AfterHeadTimeout_Answers408AndCloses` advances the fake clock only
once the server's head-timeout timer exists, the cause of its Linux failure is written down, and
a partial request head still gets the recorded 408 reply once the head timeout expires.

## Context

Split from BL-1109 (now Deferred), whose run timed out. CI run 43506096520 (PR #33, frozen
branch `factory/merge-20261113-123056`, commit e6077e2): this test failed only on
"Build and test (ubuntu-latest)", at line 26,
`CollectionAssert.AreEqual(RecordedResponse("head-timeout-408"), connection.WrittenBytes)`:
"Different number of elements". It passed on Windows and macOS, and that holds #33's merge.

- Test: `Protocols/Http/Surl.Protocol.Http.UnitTests/HeadTimeoutTests.cs:14`. It starts
  `ServeAsync` and calls `clock.Advance(HeadTimeout - 1 s)` straight away. If the server has
  not created its head-timeout timer yet, the timer starts from the advanced time and never
  fires where the test expects. A second suspect: `AdvanceUntilCompletedAsync` waits for "one
  live timer", which the just-fired head timer (not yet disposed) can satisfy before the drain
  timer exists, so the drain advance is spent on the wrong timer.
- Harness: `Protocols/Http/Surl.Protocol.Http.UnitTests/HttpServerHarness.cs` already has
  `WaitForTimersAsync(clock, count)` and `AdvanceUntilCompletedAsync`.
- Production: the head-timeout path in `Protocols/Http/Surl.Protocol.Http.UnitLibrary`. Find
  out first which side is wrong: a test that advances too early, or a server that measures the
  head timeout from the wrong moment.

Lanes test only on Windows, where this passes: reason about the race, do not rely on
reproducing it.

### Reuse BL-1109's stash

The stash `darkfactory BL-1109 20261113-143954` (find N with `git stash list`) holds the
timed-out run's work, both files tracked:

- `HttpServerHarness.cs`: a new `AdvanceUntilCompletedAfterWriteAsync(clock, serving,
  connection, length)` that waits until the connection holds `length` written bytes before
  `AdvanceUntilCompletedAsync`, so the fired head timer is gone before the drain timer is waited
  for. Review it: it spins on `Task.Yield()` with a 10-second real-time cap, like
  `WaitForTimersAsync`'s cap; that bounds a wait, it never moves the fake clock.
- `HeadTimeoutTests.cs`: `await WaitForTimersAsync(clock, 1);` before the first
  `clock.Advance`, and the helper above in place of `AdvanceUntilCompletedAsync`, in this test
  and in two others (those two belong to BL-1112; take the whole file and leave them in, or
  hand-trim them, either is fine).

Take both with:

```
git diff "stash@{N}^" "stash@{N}" -- Protocols/Http/Surl.Protocol.Http.UnitTests/HttpServerHarness.cs | git apply
git diff "stash@{N}^" "stash@{N}" -- Protocols/Http/Surl.Protocol.Http.UnitTests/HeadTimeoutTests.cs | git apply
```

Never apply, pop or drop the stash itself, and never run git checkout or git stash.

## Acceptance criteria

- [ ] The task's Log names the cause: the test advances the clock too early, the drain wait mistakes the fired head timer for the drain timer, or production starts the head timeout from the wrong time.
- [ ] `PartialHead_AfterHeadTimeout_Answers408AndCloses` advances the fake clock only after the server's head-timeout timer exists, by waiting on the fake (`WaitForTimersAsync`, or the connection having been read or written), never with `Task.Delay`, `Thread.Sleep` or real time as the thing awaited.
- [ ] `dotnet build` is clean and `dotnet test Protocols/Http/Surl.Protocol.Http.UnitTests` is green.

## Notes

Sized from BL-1109's run, which got these edits written and then ran out of time. Keep the
50-run loop and the full coverage measurement out of this task: they are BL-1112's.
BL-1110 is the same kind of race in the WebSocket tests.

## Log

- 2026-11-13: Created.
- 2026-11-13: Split from BL-1109 after its run timed out.
