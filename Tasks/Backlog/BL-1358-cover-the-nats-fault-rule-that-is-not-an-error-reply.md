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
completed:
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

- [ ] Named `Surl.Extended.Protocol.Nats.UnitTests` tests reach line 46 with an `error` rule whose reply is not an
      `-ERR` text and with a rule of another kind that is not handled before line 46, asserting each is logged as not
      applied and the command is answered normally.
- [ ] `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Nats.UnitLibrary` reports `100 | 100` with 0 failing
      members, and the fast tests are green.

## Notes

- Tests only (test-writer).

## Log

- 2026-11-17: Created.
