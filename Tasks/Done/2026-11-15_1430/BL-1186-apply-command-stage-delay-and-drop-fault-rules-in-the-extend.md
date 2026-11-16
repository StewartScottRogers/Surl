---
id: BL-1186
title: Apply command-stage delay and drop fault rules in the extended MQTT 5 broker
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1171]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests]
requirement: FR-062
created: 2026-11-14
completed: 2026-11-15
---
# BL-1186 — Apply command-stage delay and drop fault rules in the extended MQTT 5 broker

## Goal

A `delay;stage=command` or `drop;stage=command` fault rule matching an MQTT 5 packet makes the broker hold or cut off the connection there, over `mqtt5`, `mqtt5s` and `mqtt5+ws`, instead of ignoring it.

## Context

- Stand-in audit 2026-11-14: `Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary/Mqtt5BrokerConnection.Faults.cs` asks `context.Faults.FindAsync` and applies only `error` (a reason code). The original MQTT 3.1.1 server already applies delay and drop (`Surl.Protocol.Mqtt.UnitLibrary`, `CommandStageFaults`) - follow it. Call `CommandStageFaults.FindAndApplyAsync` (ADR-0141) at the same point so delay and drop apply and an `error` is handed back.
- Depends on BL-1171 only because both change this library.
- Judged by the MQTT 5.0 specification (ADR-0083, ADR-0093).

## Acceptance criteria

- [x] Named tests: a `delay` rule for `PUBLISH` holds it for `ms` on a fake `TimeProvider`, pausing and restarting the idle clock, then handles it as usual; one `FaultAppliedEvent` (`Delay`, `Command`).
- [x] Named tests: a `drop` rule for `SUBSCRIBE` aborts the connection with no `SUBACK`; one `FaultAppliedEvent` (`Drop`, `Command`).
- [x] Every existing chosen-error test passes unchanged, and a rule for another packet or topic changes nothing.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage with no method over complexity 10.

## Notes

Drop sets a per-connection flag (`droppedByFault`) that PUBLISH and SUBSCRIBE handling read, ending the connection abnormally (Will published). Replaced the old "delay is not applied" test.

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. MQTT 5 broker applies command-stage delay and drop rules; tests green
