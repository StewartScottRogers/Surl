---
id: BL-1922
title: Fix the flaky QUIC closing-state test that can wait forever on the fake clock
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Quic.UnitLibrary, Protocols/Http/Surl.Quic.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1922 — Fix the flaky QUIC closing-state test that can wait forever on the fake clock

## Goal

`QuicDatagramFlowServerTests.ServeAsync_ClientSendsInTheClosingState_AnswersWithTheConnectionCloseAgainAndEndsOnceTheClockPassesTheStatesEnd` passes however the threads are scheduled.

## Context

Seen in BL-1916 (2026-11-27): in a full fast run the test failed after 30 s, and `Surl.Quic.UnitTests` passed 514 of 514 when run again on its own. A likely cause, read from the test and not confirmed: `client.Time.Advance(TimeSpan.FromMinutes(1))` can run before the server has armed the closing state's timer on the fake `TimeProvider`, so the state never ends and `serving.WaitAsync` times out. `ServeAsync_ClientClosesTheConnection_CancelsEndedBeforeTheDrainingStateEnds` in `Protocols/Http/Surl.Quic.UnitTests/QuicDatagramFlowServerTests.cs` has the same shape. CI runs these tests on Windows, Linux and macOS, and a red job blocks the merge to `master`. No `Thread.Sleep` (`.claude/rules/testing.md`).

## Acceptance criteria

- [x] The cause of the timeout is established and written under Notes.
- [x] The closing-state and draining-state tests wait for what they depend on before advancing the clock, or the server code that caused the race is fixed with a test that reproduces it.
- [x] `dotnet build -warnaserror` is clean and the fast tests of `Protocols/Http/Surl.Quic.UnitTests` pass in 20 consecutive runs.

## Notes

- Cause: `QuicDatagramFlowLoop.WaitAndActAsync` read the clock to work out the delay
  (`DelayUntil`) and then armed the timer (`Task.Delay`). A clock that jumps between the two
  arms the timer at the new time plus the old delay, past the deadline, and on a clock that then
  stands still it never fires. The test advances the clock as soon as it sees the repeated
  `CONNECTION_CLOSE`, while the loop may still be on its way to arming that timer.
- Reproduced: `ServeAsync_ClockPassesTheClosingStatesEndAsItsTimerIsArmed_Ends` moves the clock
  from `UnitTestTimeProvider.BeforeTimerIsArmed` on the timer armed after the repeated close, and
  timed out after 30 s before the fix. A jump on the first closing-state timer does not reproduce
  it, because the change signal `EndAsync` raised wakes the loop and it reads the clock again.
- Fix: the server code, not the tests, because any test that moves a fake clock (a stand-in
  embedded through `Surl.Api` too) could hit it. `WaitUntilAsync` reads the clock again once the
  timer is armed and completes at once when the deadline has passed. The same method waits for
  the draining state, so both existing tests are covered and are unchanged.
- Verified: build `-warnaserror` clean, format check clean, all fast tests passed (30314 passed,
  3 skipped, 0 failed), `Surl.Quic.UnitTests` 515 of 515 in 20 consecutive runs. Integration tests
  not run: the change opens no socket or file.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The QUIC flow loop ends the closing or draining state even when the clock jumps as its timer is armed; the flaky test can no longer wait forever
