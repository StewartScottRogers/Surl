---
id: BL-1359
title: Cover the last SignalR AcceptMessageAsync branch
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Http/Surl.Extended.Protocol.SignalR.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-1359 — Cover the last SignalR AcceptMessageAsync branch

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.SignalR.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 100% line, 99.89% branch, 1 failing member.
- `SignalRHubSession.AcceptMessageAsync` (async) -
  `Protocols/Http/Surl.Extended.Protocol.SignalR.UnitLibrary/SignalRHubSession.cs:369`, complexity 10 (at the gate),
  94.44% branch; the `switch (message)` at line 375 is 15 of 16 - one case/guard combination (for example
  `case HubAck or HubSequence when reconnect is not null` with `reconnect` null) is never taken.

## Acceptance criteria

- [ ] A named `Surl.Extended.Protocol.SignalR.UnitTests` test sends the hub message that takes the missing edge of the
      line 375 switch (identify it from the per-line Cobertura report) and asserts the session's answer.
- [ ] `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.SignalR.UnitLibrary` reports `100 | 100` with 0 failing
      members, and the fast tests are green.

## Notes

- Tests only (test-writer). The method is at complexity 10, the gate; anything that adds a branch to it must split it.

## Log

- 2026-11-17: Created.
