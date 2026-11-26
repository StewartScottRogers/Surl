---
id: BL-1726
title: Make the MQTT 5 publish delay fault test hold deterministically
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1726 — Make the MQTT 5 publish delay fault test hold deterministically

## Goal

`Mqtt5BrokerTests.ServeAsync_APublishDelayRule_HoldsThePublishForTheDelayWithTheIdleClockPausedThenHandlesItAsUsual`
passes every time, also on a loaded machine.

## Context

- Seen on 2026-11-25 on Windows: in one full fast-test run it failed at
  `Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests/Mqtt5BrokerTests.Faults.cs` line 145,
  `Assert.IsTrue(pausedAtTheHold)` (actual `false`). It passed in the previous full run and in three reruns of the
  project alone, so it races under load.
- Likely cause, read from code and not reproduced: the test builds its connection with
  `Mqtt5BrokerExchange.ConnectionSending(staysOpen: false, ...)`, which gives an `InMemoryConnection` with
  `peerHalfClosesWhenExhausted: true`. Its `WatchForPeerCloseAsync` then returns `PeerClosed` at once, so
  `CommandStageFaults` (in `Foundation/Surl.Protocol.Abstractions.UnitLibrary`, ADR-0141 amendment 2) ends the hold
  right after `IdleClock.Pause()` and calls `Restart()` without the test's clock advancing. The test, resumed by
  `RecordingIdleClock.FirstPause`, races that `Restart()`. The Abstractions library's `CLAUDE.md` says a test of a
  full-length hold passes `peerHalfClosesWhenExhausted: false`.
- So the expected fix is in the test: a client that stays (`staysOpen: true`; the script's `DISCONNECT` still ends
  the exchange), and assertions that show the hold lasts until the clock reaches the delay. Inject time through
  `ManualTimeProvider`; no `Thread.Sleep` and no real-time wait other than the existing safety timeouts.
- If the cause turns out to be in `Surl.Extended.Protocol.Mqtt5.UnitLibrary`, add that folder to `touches`, fix it
  there with a test that fails without the fix, and say so in the Log.
- Stand-in: test-only work; no store, event or fault behaviour changes.

## Acceptance criteria

- [x] The named test uses a connection whose peer does not half-close while the delay is held.
- [x] It advances the clock by 499 ms and asserts that serving has not completed, nothing is published and the idle
      clock is still paused; after 1 ms more it asserts the publish, the `PUBACK`, the restarted idle clock and the one
      `FaultAppliedEvent`.
- [x] The test file contains no `Thread.Sleep` and no `Task.Delay`.
- [x] `dotnet test Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests` passes 20 times in a row, and the full
      fast tests are green.

## Notes

- Cause confirmed by reading `CommandStageFaults` and `InMemoryConnection`: a half-closing script makes the watch
  return `PeerClosed` at once, so the hold ended without the clock moving and `Restart()` raced the test. The fix is
  test-only (`staysOpen: true`); no library changed, so no ADR.
- The test also asserts one timer still pending at 499 ms. Why: with the rule temporarily set to 499 ms, the
  "not completed", "nothing published" and "still paused" assertions all passed, because the server resumes on
  another thread after the timer fires. The timer count is read synchronously and failed that mutation as it should.
- Verified 2026-11-25 on Windows: build 0 warnings, format check clean, fast tests 29144 passed, 0 failed,
  3 skipped; the MQTT 5 project 444 of 444, 20 runs in a row.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. The MQTT 5 publish delay fault test holds for the full delay on the manual clock and no longer races the idle clock restart.
