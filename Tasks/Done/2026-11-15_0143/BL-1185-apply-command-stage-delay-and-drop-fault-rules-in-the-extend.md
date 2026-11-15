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
completed: 2026-11-14
---
# BL-1185 — Apply command-stage delay and drop fault rules in the extended AMQP 1.0 container

## Goal

A `delay;stage=command` or `drop;stage=command` fault rule matching an AMQP 1.0 performative makes the container hold or cut off the connection there, instead of ignoring it.

## Context

- Stand-in audit 2026-11-14: `Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary/Amqp1ContainerConnection.Faults.cs` asks `context.Faults.FindAsync` and applies only `error` (an error condition). Call `CommandStageFaults.FindAndApplyAsync` (`Surl.Protocol.Abstractions`, ADR-0141) at the same point so delay and drop apply and an `error` is handed back.
- Judged by the OASIS AMQP 1.0 specification (ADR-0083).

## Acceptance criteria

- [x] Named tests: a `delay` rule for `attach` (or `transfer`) holds it for `ms` on a fake `TimeProvider`, pausing and restarting the idle clock, then handles it as usual; one `FaultAppliedEvent` (`Delay`, `Command`).
- [x] Named tests: a `drop` rule aborts the connection with no answering performative; one `FaultAppliedEvent` (`Drop`, `Command`).
- [x] Every existing chosen-error test passes unchanged, and a rule for another performative or address changes nothing.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch coverage with no method over complexity 10.

## Notes

- `FindChosenErrorAsync` now calls `CommandStageFaults.FindAndApplyAsync(context, connection, point, target)`. A drop aborts the connection, sets `closed` and throws the internal `Amqp1ConnectionDroppedException`, caught in `ServeUntilEndAsync`, so no handler writes an answer and the normal cleanup runs. Chosen: an exception over a flag because `attach` and `transfer` handlers sit deep in the answer path and would otherwise write to the aborted connection.
- The one test that asserted a delay rule was "not applied" (`ADelayRuleAtTheCommandStage_IsNotApplied`) contradicted this task's goal and was replaced by the delay tests; every chosen-error test is unchanged. Added a test for a rule kind an attach cannot apply (`OutOfSpace`), which the old delay test used to cover for branch coverage.
- Tests: 812 in the Amqp1 test project pass; Measure-CodeQuality reports 100% line and branch for the library.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Done. Command-stage delay and drop fault rules now apply to AMQP 1.0 attach and transfer
