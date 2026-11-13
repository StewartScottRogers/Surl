---
id: BL-1112
title: Give the other HTTP head-timeout tests the timer wait and prove HeadTimeoutTests stable over 50 runs
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1111]
touches: [Protocols/Http/Surl.Protocol.Http.UnitLibrary, Protocols/Http/Surl.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-13
completed:
---
# BL-1112 — Give the other HTTP head-timeout tests the timer wait and prove HeadTimeoutTests stable over 50 runs

## Goal

Every `HeadTimeoutTests` test that advances the fake clock waits for the server's timer first,
and the class passes 50 runs in a row on Windows with the solution green and
Surl.Protocol.Http.UnitLibrary at 100% line and branch coverage.

## Context

Split from BL-1109 (now Deferred), whose run timed out. BL-1111 fixes
`PartialHead_AfterHeadTimeout_Answers408AndCloses` and names the race; this task gives the same
wait to the rest of `Protocols/Http/Surl.Protocol.Http.UnitTests/HeadTimeoutTests.cs`:

- `NoBytes_AfterHeadTimeout_ClosesWithNoBytes` calls `clock.Advance(HeadTimeout)` straight
  after `ServeAsync`.
- `KeptAliveConnection_NextHeadIsTimedFromItsFirstByte` advances straight after its timer
  assert, then calls `AdvanceUntilCompletedAsync`, which can take the fired head timer for the
  drain timer.
- Leave alone the tests that never wait on a timer firing (`IsNotCutOffByTheHeadTimeout`,
  `ExchangeCancelledDuringAHead`, `InfiniteOrLongerThanATimerCanWaitHeadTimeout`) unless
  BL-1111's cause shows they race too.

### Reuse BL-1109's stash

If BL-1111 took the whole of `HeadTimeoutTests.cs` from the stash
`darkfactory BL-1109 20261113-143954`, these two tests already carry
`await WaitForTimersAsync(clock, 1);` and `AdvanceUntilCompletedAfterWriteAsync`; check them
against BL-1111's cause. If it trimmed them, take the file's hunks again with
`git diff "stash@{N}^" "stash@{N}" -- Protocols/Http/Surl.Protocol.Http.UnitTests/HeadTimeoutTests.cs | git apply`
(N from `git stash list`; resolve against BL-1111's version by hand if it does not apply).
In `KeptAliveConnection_NextHeadIsTimedFromItsFirstByte` the stash's `WaitForTimersAsync`
straight after `Assert.AreEqual(1, clock.ActiveTimerCount, ...)` is redundant; drop it.
Never apply, pop or drop the stash itself, and never run git checkout or git stash.

### Keep the run short

BL-1109 ran out of time. Build once, then loop with `--no-build`:

```
dotnet build Protocols/Http/Surl.Protocol.Http.UnitTests
1..50 | % { dotnet test Protocols/Http/Surl.Protocol.Http.UnitTests --no-build --filter "FullyQualifiedName~HeadTimeoutTests" ; if ($LASTEXITCODE) { "fail on run $_"; break } }
```

## Acceptance criteria

- [ ] `NoBytes_AfterHeadTimeout_ClosesWithNoBytes` and `KeptAliveConnection_NextHeadIsTimedFromItsFirstByte` advance the fake clock only after the timer they fire exists, waiting on the fake, never on `Task.Delay`, `Thread.Sleep` or real time.
- [ ] `HeadTimeoutTests` passes 50 runs in a row on Windows: `dotnet test Protocols/Http/Surl.Protocol.Http.UnitTests --filter "FullyQualifiedName~HeadTimeoutTests"` looped 50 times, the count recorded in the Log.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` is green. Surl.Protocol.Http.UnitLibrary keeps 100% line and branch coverage.

## Notes

BL-1110 is the same kind of race in the WebSocket tests.

## Log

- 2026-11-13: Created.
- 2026-11-13: Split from BL-1109 after its run timed out.
- 2026-11-13: Backlog -> Doing.
