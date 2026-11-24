---
id: BL-1621
title: Make ServingEngineTests.ServeAsync_ClientResetsDuringAnAcceptDelay_AbortsTheExchangeAtOnce_AndFreesItsSlot deterministic
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Foundation/Surl.Core.UnitTests]
requirement: none
created: 2026-11-23
completed: 2026-11-24
---
# BL-1621 — Make ServingEngineTests.ServeAsync_ClientResetsDuringAnAcceptDelay_AbortsTheExchangeAtOnce_AndFreesItsSlot deterministic

## Goal

The accept-delay peer-leaving tests in `Surl.Core.UnitTests` pass every run on every platform.

## Context

- PR #145 (the v0.1.7 releases rows, which change only a Markdown file), CI run 45243738165, windows-latest:
  `ServeAsync_ClientResetsDuringAnAcceptDelay_AbortsTheExchangeAtOnce_AndFreesItsSlot` threw "The operation has timed
  out" after 10 s at `Foundation/Surl.Core.UnitTests/ServingEngineTests.AcceptHoldPeerLeaving.cs:85`. The same test
  passed on ubuntu and macos in that run and on every platform in PR #144. The failed job was rerun.
- The test comes from BL-1521 (an accept-stage delay hold ends when the client leaves, ADR-0141 amendment 2). It
  times out waiting for the exchange to end after the fake client resets. Likely a race between the reset reaching
  the peer watch (`IConnection.WatchForPeerCloseAsync`) and the hold starting, or the manual clock being advanced
  before the hold is registered. The same family as BL-1556, BL-1566 and BL-1569, but here the client *does* leave
  and the hold doesn't notice.

## Acceptance criteria

- [x] The Log names the race with file:line (test or engine).
- [x] The test waits for the hold to be in place (pending timer or a hold-started signal) before the client
      resets, or the engine is fixed if a reset that lands before the watch starts can be missed. The latter would be
      a real bug: a slot held for the full delay. If so, it's fixed with a named fast test.
- [x] The sibling tests in `ServingEngineTests.AcceptHoldPeerLeaving.cs` get the same wait.
- [x] The changed tests pass 50 runs in a row locally
      (`--filter "FullyQualifiedName~AcceptHoldPeerLeaving"` in a loop).
- [x] `dotnet build` clean and the full fast suite green.

## Notes

Cause (test, not engine): `ConnectUntilAdmittedAsync` in `ServingEngineTests.AcceptHoldPeerLeaving.cs` treated a connection as admitted if not disposed 20 ms after connecting; on a slow runner a connection about to be refused looked admitted, so `time.Advance` (line 84 then) fired no hold timer and `NextExchangeAsync` timed out. The engine does catch a reset before the watch starts (`InMemoryConnection` and `StreamConnection` report it on the first watch), so no engine bug. Fix: admitted now means the hold timer exists (`ManualTimeProvider.TimerCount` rose) and the connection is not disposed. Both tests that use the helper get it. 50 of 50 local runs of the six accept-hold tests pass; full fast suite green.

## Log

- 2026-11-23: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Fixed; Doing -> Done.
- 2026-11-24: Doing -> Done. Accept-hold reset test waits for the hold timer before advancing the clock; 50/50 local runs pass
