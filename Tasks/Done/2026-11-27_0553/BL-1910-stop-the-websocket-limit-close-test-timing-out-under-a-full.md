---
id: BL-1910
title: Stop the WebSocket limit-close test timing out under a full test run
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1910 — Stop the WebSocket limit-close test timing out under a full test run

## Goal

`WsClosingTests.ExchangeCancelledForALimit_IsClosedWith1001_ThenLingered` passes in a full fast-test run under load.

## Context

- Its `"maximum duration"` row failed in a full fast-test run on Windows on 2026-11-27, during BL-1908:
  `System.TimeoutException` after 30 s from `WsServerHarness.EndLingeringCloseAsync` (`WsServerHarness.cs:142`),
  called at `WsClosingTests.cs:77`. The project run alone passed (136 of 136), and so did a second full run.
- BL-1110 fixed an earlier race in the same test: the harness now waits for 2 created timers
  (`ManualTimeProvider.CreatedTimerCount`) before it advances the clock. The cause of this failure is not known.

## Acceptance criteria

- [x] The task's Notes name the cause of the timeout.
- [x] The test waits only on signals that show the server is ready for the next step, with no real-time delay added.
- [x] `dotnet test Protocols/Http/Surl.Protocol.Ws.UnitTests` passes.

## Notes

- **Cause:** the harness waited for the wrong timer. The limit-close exchange creates three timers, not two: the head
  timeout's (`HttpConnectionReader.ReadNextRequestHeadAsync`), the `CLOSE` 1001 write deadline's, then the lingering
  close's. BL-1110's wait for `CreatedTimerCount >= 2` was met by the write deadline. Its timer is still live when
  `CompleteWritesAsync` sets `WritesCompleted`, so `ActiveTimerCount == 1` held too. Under load the test advanced the
  clock in that window; the linger's timer was then created after the advance, never fell due, and `serving` timed out
  after 30 s.
- `WsHeadLimitTests.PartialHead_AfterHeadTimeout_IsAnswered408` had the same race (head timeout, refusal write
  deadline, linger, while it waited for 1) and is fixed with the same count.
- **Reproduced:** with a temporary `Task.Delay(200)` after `CompleteWritesAsync` in
  `WebSocketExchange.TryWriteGoingAwayCloseAsync` and `WebSocketUpgradeResponder.TryWriteRefusalAsync`, 3 of 136
  failed with that `TimeoutException` (both limit rows and the 408 test). With the fix and the delays still in, 136 of
  136 passed. The delays were then removed; no production file changed.
- **Fix:** `WsServerHarness` names the two counts, `TimersCreatedWhenLingering` (2, the default) and
  `TimersCreatedWhenLingeringAfterAWriteDeadline` (3). `WaitForLingeringCloseAsync` now asserts the half-close and the
  exact created count after its poll, so a poll that runs out fails there instead of advancing the clock early.
- Full verify on 2026-11-27: build 0 warnings, format clean, fast tests 30275 passed, 0 failed, 3 skipped.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The WebSocket limit-close and 408 tests advance the clock only once the lingering close's own timer exists.
