---
id: BL-1185
title: Apply command-stage delay and drop fault rules in the extended AMQP 1.0 container
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests]
requirement: FR-062
created: 2026-11-14
completed:
---
# BL-1185 — Apply command-stage delay and drop fault rules in the extended AMQP 1.0 container

## Goal

A `delay;stage=command` or `drop;stage=command` fault rule matching an AMQP 1.0 performative makes the container hold or cut off the connection there, instead of ignoring it.

## Context

- Stand-in audit 2026-11-14: `Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/Amqp1ContainerConnection.Faults.cs` asks `context.Faults.FindAsync` and applies only `error` (an error condition). Call `CommandStageFaults.FindAndApplyAsync` (`Surl.Protocol.Abstractions`, ADR-0141) at the same point so delay and drop apply and an `error` is handed back.
- Judged by the OASIS AMQP 1.0 specification (ADR-0083).

## Acceptance criteria

- [ ] Named tests: a `delay` rule for `attach` (or `transfer`) holds it for `ms` on a fake `TimeProvider`, pausing and restarting the idle clock, then handles it as usual; one `FaultAppliedEvent` (`Delay`, `Command`).
- [ ] Named tests: a `drop` rule aborts the connection with no answering performative; one `FaultAppliedEvent` (`Drop`, `Command`).
- [ ] Every existing chosen-error test passes unchanged, and a rule for another performative or address changes nothing.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage with no method over complexity 10.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
