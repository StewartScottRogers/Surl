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
completed:
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

- [ ] The task's Notes name the cause of the timeout.
- [ ] The test waits only on signals that show the server is ready for the next step, with no real-time delay added.
- [ ] `dotnet test Protocols/Http/Surl.Protocol.Ws.UnitTests` passes.

## Notes

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
