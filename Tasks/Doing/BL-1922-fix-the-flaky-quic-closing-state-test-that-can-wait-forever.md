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
completed:
---
# BL-1922 — Fix the flaky QUIC closing-state test that can wait forever on the fake clock

## Goal

`QuicDatagramFlowServerTests.ServeAsync_ClientSendsInTheClosingState_AnswersWithTheConnectionCloseAgainAndEndsOnceTheClockPassesTheStatesEnd` passes however the threads are scheduled.

## Context

Seen in BL-1916 (2026-11-27): in a full fast run the test failed after 30 s, and `Surl.Quic.UnitTests` passed 514 of 514 when run again on its own. A likely cause, read from the test and not confirmed: `client.Time.Advance(TimeSpan.FromMinutes(1))` can run before the server has armed the closing state's timer on the fake `TimeProvider`, so the state never ends and `serving.WaitAsync` times out. `ServeAsync_ClientClosesTheConnection_CancelsEndedBeforeTheDrainingStateEnds` in `Protocols/Http/Surl.Quic.UnitTests/QuicDatagramFlowServerTests.cs` has the same shape. CI runs these tests on Windows, Linux and macOS, and a red job blocks the merge to `master`. No `Thread.Sleep` (`.claude/rules/testing.md`).

## Acceptance criteria

- [ ] The cause of the timeout is established and written under Notes.
- [ ] The closing-state and draining-state tests wait for what they depend on before advancing the clock, or the server code that caused the race is fixed with a test that reproduces it.
- [ ] `dotnet build -warnaserror` is clean and the fast tests of `Protocols/Http/Surl.Quic.UnitTests` pass in 20 consecutive runs.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
