---
id: BL-1358
title: Cover the NATS fault rule that is not an error reply
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-1358 — Cover the NATS fault rule that is not an error reply

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Nats.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 100% line, 99.65% branch, 1 failing member.
- `NatsServerConnection.IsAnsweredByAFaultAsync` (async) -
  `Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary/NatsServerConnection.Faults.cs:17`, 87.5% branch; line 46
  is 3 of 4: `rule.Kind != FaultKind.Error || !IsErrorText(rule.Reply)` never sees one combination (an `Error` rule whose
  reply is not a NATS `-ERR` text, or a non-error, non-delay, non-drop kind reaching line 46).

## Acceptance criteria

- [x] Named `Surl.Extended.Protocol.Nats.UnitTests` tests reach line 46 with an `error` rule whose reply is not an
      `-ERR` text and with a rule of another kind that is not handled before line 46, asserting each is logged as not
      applied and the command is answered normally.
- [x] `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Nats.UnitLibrary` reports `100 | 100` with 0 failing
      members, and the fast tests are green.

## Notes

- Tests only. Added NatsFaultTests.ServeAsync_ARuleOfAKindThatIsNotAnError_IsNotedAndNotApplied (RefuseLogin, OutOfSpace); the error-with-bad-reply case already existed.

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing.
- 2026-11-17: Doing -> Done. NATS fault rule of a non-error kind now covered; Nats library at 100/100 with 0 failing members
