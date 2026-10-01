---
id: BL-354
title: Find why Surl.Protocol.Ws.UnitTests intermittently hangs under the full fast-test run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Surl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-354 — Find why Surl.Protocol.Ws.UnitTests intermittently hangs under the full fast-test run

## Goal

`dotnet test --filter "TestCategory!=Integration"` never hangs in `Surl.Protocol.Ws.UnitTests`:
the test that hung is found and its race fixed or its wait bounded.

## Context

- 2026-10-01, lane 1 (BL-353): the full fast-test run on Windows hung for over an hour in
  `Surl.Protocol.Ws.UnitTests` (its testhost still alive; every other project had passed).
  Run alone straight after, it passed 184/184 in 285 ms, so the hang is intermittent and
  likely load- or order-dependent (other lanes were building and testing on the machine).
- Start with `dotnet test Surl.Protocol.Ws.UnitTests --blame-hang-timeout 2m` in a loop
  under load, and look for an unbounded await on a stream read or a `TaskCompletionSource`
  awaited without cancellation.

## Acceptance criteria

- [x] The test that hung is named under Notes, with its cause.
- [x] Every wait in `Surl.Protocol.Ws.UnitTests` is bounded by a cancellation token or
      timeout, so a regression fails instead of hanging.
- [x] `dotnet test Surl.Protocol.Ws.UnitTests --blame-hang-timeout 2m` passes 20 runs in a row.

## Notes

- **Cause: a race in the test clock, `ManualTimeProvider`, not in the server.** `CreateTimer` added the
  timer to its list and only then called `Change`, which set `DueAt`. The tests that wait with
  `WaitForTimersAsync` / `WaitForLingeringCloseAsync` and then call `clock.Advance` poll
  `ActiveTimerCount` from the test thread while the server creates its timer on a thread-pool thread
  (it resumes there after a cancelled read or a cancelled stalled write). Under load the test could see
  the count reach 1, call `Advance` while `DueAt` was still null - firing nothing - and `Change` then set
  `DueAt` to the already-advanced clock plus the delay, so the timer never fired, the exchange never
  ended and `await serving` waited forever with no test timeout to stop it.
- **The tests that could hang that way** (any of them may have been the one on 2026-10-01; the hung run
  left no blame log to name it): in `WsClosingTests`, `ExchangeCancelledForALimit_IsClosedWith1001_ThenLingered`
  (both rows), `Close1001_NotWrittenWithinOneSecond_IsGivenUp`, `Refusal_NotWrittenWithinOneSecond_IsGivenUp`
  and `ExchangeCancelled_WhileARefusalIsWritten_EndsTheExchange` pass `WaitForTimersAsync` before the timer
  can be fully set; in `WsHeadLimitTests`, `PartialHead_AfterHeadTimeout_IsAnswered408`, whose linger
  timer is created on the thread pool after the head timeout fires. The most likely is
  `ExchangeCancelledForALimit_IsClosedWith1001_ThenLingered`, the one whose lingering-close timer is always
  created on a thread-pool thread right as the test polls for it.
- **Fix:** `ManualTimeProvider.CreateTimer` sets `DueAt` and adds the timer under one lock; `Change`
  takes that lock; `Advance` clears each due timer's `DueAt` under it before firing. A counted timer
  now always has its due time.
- **Bounded waits:** every wait on an exchange goes through `WsServerHarness.WithinTimeout`, which fails
  with `TimeoutException` after `ExchangeEndTimeout` (30 s; every exchange here ends in milliseconds).
  The polling helpers were already bounded at 10 s, and `StalledWriteConnection`'s stalled write ends on
  its cancellation token. Chose a harness helper over `[Timeout]` on every method: it bounds exactly
  the awaits that can hang, adds no attribute to 184 tests, and needs no runsettings.
- No regression test reproduces the race: the window was between two statements in the fake and
  cannot be held open deterministically; the fix removes it by construction.
- 20 runs of `dotnet test Surl.Protocol.Ws.UnitTests --blame-hang-timeout 2m` passed in a row,
  184/184 each.
- Follow-up filed: BL-355, the same add-then-`Change` order in 15 other test projects'
  `ManualTimeProvider` copies.
## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. Ws tests' fake clock no longer races the server's timer creation, and every wait on an exchange is bounded at 30 s; 20 hang-checked runs passed
