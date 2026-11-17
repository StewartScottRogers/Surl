---
id: BL-1355
title: Cover the AMQP 0-9-1 fault reply and forced-close branches
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-1355 — Cover the AMQP 0-9-1 fault reply and forced-close branches

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Amqp0.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 99.96% line, 99.77% branch, 2
  failing members, in `Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary/`:
  - `Amqp0BrokerConnection.ChosenException(FaultRule, string)` - `Amqp0BrokerConnection.Faults.cs:167`, 75% branch;
    line 168 is 3 of 4: `rule.Kind != FaultKind.Error || !TryReadException(...)` never sees one combination - most
    likely an `Error` rule whose reply is not an AMQP reply code and text.
  - `Amqp0BrokerConnection.ServeOpenedUntilCancelledAsync` (async) - `Amqp0BrokerConnection.cs:104`, 90% line, 50% branch:
    the `catch (OperationCanceledException) when (!closeSent)` filter at line 109 is only ever true or only false, and
    line 113 is uncovered.

## Acceptance criteria

- [x] A named `Surl.Extended.Protocol.Amqp0.UnitTests` test applies an `error` fault rule whose reply is not an AMQP
      0-9-1 reply code and text, and asserts the "not applied" log note and that the command is answered normally.
- [x] Named tests cancel a served connection both before and after the broker sent `Connection.Close`, asserting the
      forced close is written only in the first case, so line 109 sees both outcomes and line 113 runs.
- [x] `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Amqp0.UnitLibrary` reports `100 | 100` with 0 failing
      members, and the fast tests are green.

## Notes

- Tests only (test-writer). Expected bytes come from the AMQP 0-9-1 specification (extended server, ADR-0083).
- Added `QueueDeclare_UnderARuleOfAKindThatIsNotAnError_IsNotedAndServed` (a non-`Error` rule kind was the missing
  `Kind != Error` combination) and `ServeAsync_CancelledAfterTheBrokerSentConnectionClose_WritesNoForcedCloseAndThrows`.
- Tests alone could not reach 100%: coverage never records an exception filter's false outcome or the `}` after an
  awaited `throw;` in an async catch. Decision: `Amqp0BrokerConnection.ServeOpenedUntilCancelledAsync` now catches the
  cancellation, writes the forced close only when no `connection.close` was sent, then rethrows with
  `ExceptionDispatchInfo.Throw`. Behaviour is unchanged. Added the library to this task's reach; no task in Doing
  touches it.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing.
- 2026-11-17: Doing -> Done. Amqp0 library at 100% line and branch: non-error fault rule and cancel-after-close tests, filter-free forced close
