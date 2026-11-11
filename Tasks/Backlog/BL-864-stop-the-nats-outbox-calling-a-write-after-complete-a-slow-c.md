---
id: BL-864
title: Stop the NATS outbox calling a late write after Complete a slow consumer
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1035]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-864 — Stop the NATS outbox calling a late write after Complete a slow consumer

## Goal

`NatsOutbox.Enqueue` after `Complete` drops the bytes without setting `HasOverflowed`, so a connection that is ending is
never answered `-ERR 'Slow Consumer'` unless it really passed the pending-bytes bound.

## Context

- `NatsOutbox.Enqueue` falls through to `Interlocked.Exchange(ref overflowed, 1)` whenever `TryWrite` fails, which it
  also does once the channel is completed, so a bus delivery racing the connection's end marks the outbox overflowed and
  `NatsServerConnection.ServeAsync` then writes the slow-consumer error (ADR-0113 decision 7).
- BL-532 found and fixed the same flaw in its copy, `StompOutbox.Enqueue`: overflow only when the pending bytes pass the
  bound. Follow that fix.

## Acceptance criteria

- [ ] A test named `NatsOutboxTests.Enqueue_AfterComplete_IsDroppedWithoutOverflowing` (or the class's existing
      equivalent) passes: `Complete`, then `Enqueue`, leaves `HasOverflowed` false.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- 2026-11-11 (lane 6): The fix and `NatsOutboxTests` are on branch `factory/BL-864-lane-3-20261111-060243`
  (commit 893fa7fb). They follow BL-532's `StompOutbox.Enqueue`: the outbox overflows only past the pending-bytes
  bound. Lane 3's integration failed because `dotnet build` cannot run in the repository: `global.json` pins
  SDK 10.0.401 and the machine has only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 (exit 155, SDK not found). The
  task waits on BL-1035. Once that is done, cherry-pick the code files from that commit, then build and run the
  fast tests with the pinned SDK.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 3 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-864-lane-3-20261111-060243; start with git cherry-pick --no-commit factory/BL-864-lane-3-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run; fix is on factory/BL-864-lane-3-20261111-060243
