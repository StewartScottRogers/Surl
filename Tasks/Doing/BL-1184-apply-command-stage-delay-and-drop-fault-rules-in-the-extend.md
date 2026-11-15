---
id: BL-1184
title: Apply command-stage delay and drop fault rules in the extended AMQP 0-9-1 broker
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1170]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests]
requirement: FR-062
created: 2026-11-14
completed:
---
# BL-1184 — Apply command-stage delay and drop fault rules in the extended AMQP 0-9-1 broker

## Goal

A `delay;stage=command` or `drop;stage=command` fault rule matching an AMQP 0-9-1 method makes the broker hold or cut off the connection at that method, instead of logging "not applied".

## Context

- Stand-in audit 2026-11-14: `Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary/Amqp0BrokerConnection.Faults.cs` asks `context.Faults.FindAsync` at each method (command and path as today) and drops any rule that is not an `error` with a reply code. Call `CommandStageFaults.FindAndApplyAsync` (`Surl.Protocol.Abstractions`, ADR-0141) at the same point so delay and drop apply and an `error` is handed back to today's code.
- Depends on BL-1170 only because both change this library.
- Judged by the AMQP 0-9-1 specification (ADR-0083).

## Acceptance criteria

- [ ] Named tests: a `delay` rule for `basic.publish` holds that method for `ms` on a fake `TimeProvider`, pausing and restarting the idle clock, then handles it as usual; one `FaultAppliedEvent` (`Delay`, `Command`).
- [ ] Named tests: a `drop` rule for `queue.declare` aborts the connection with no `queue.declare-ok` sent; one `FaultAppliedEvent` (`Drop`, `Command`).
- [ ] Every existing chosen-error test passes unchanged, and a rule for another method changes nothing.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage with no method over complexity 10.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing.
