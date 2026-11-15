---
id: BL-1187
title: Apply command-stage delay and drop fault rules in the extended NATS server
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests]
requirement: FR-062
created: 2026-11-14
completed:
---
# BL-1187 — Apply command-stage delay and drop fault rules in the extended NATS server

## Goal

A `delay;stage=command` or `drop;stage=command` fault rule matching a NATS protocol operation makes the server hold or cut off the connection there, instead of ignoring it.

## Context

- Stand-in audit 2026-11-14: `Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary/NatsServerConnection.Faults.cs` asks `context.Faults.FindAsync` and applies only `error` (`-ERR '<text>'`). Call `CommandStageFaults.FindAndApplyAsync` (`Surl.Protocol.Abstractions`, ADR-0141) at the same point so delay and drop apply and an `error` is handed back. Note the NATS outbox: a drop must abort without flushing anything queued after the matching operation.
- Judged by the NATS client protocol documentation (ADR-0083).

## Acceptance criteria

- [ ] Named tests: a `delay` rule for `PUB` holds it for `ms` on a fake `TimeProvider`, pausing and restarting the idle clock, then delivers as usual; one `FaultAppliedEvent` (`Delay`, `Command`).
- [ ] Named tests: a `drop` rule for `SUB` aborts the connection with no `+OK` (in verbose mode) or other reply; one `FaultAppliedEvent` (`Drop`, `Command`).
- [ ] Every existing chosen-error test passes unchanged, and a rule for another operation or subject changes nothing.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage with no method over complexity 10.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
