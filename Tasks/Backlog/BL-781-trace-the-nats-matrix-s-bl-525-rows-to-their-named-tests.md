---
id: BL-781
title: Trace the NATS matrix's BL-525 rows to their named tests
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-525]
touches: [Documentation/Conformance/Extended/Nats.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-781 — Trace the NATS matrix's BL-525 rows to their named tests

## Goal

Every row of `Documentation/Conformance/Extended/Nats.md` whose "Covered by" says `BL-525` names
the tests that prove it instead.

## Context

- The matrix's own rule (its introduction, ADR-0113 "Consequences"): each task replaces its ID
  with the named test. BL-525 built the server and its tests but could not edit the matrix,
  because BL-394 was in `Doing` touching `Documentation/Conformance` at the time.
- The tests are in `Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests`:
  `NatsServerConnectionTests`, `NatsServerTests`, `NatsInfoTests`, `NatsConnectOptionsTests`,
  `NatsControlLineReaderTests`, `NatsControlLineTests` and `NatsServerOptionsTests`. Their names
  state the row each proves (e.g. `ServeAsync_LowerCasePing_IsAnsweredPong` for NATS-PROTO-1
  and NATS-PING-1; `WriteRefusalAsync_EitherLimit_IsMaximumConnectionsExceededWithNoInfoThenClosed`
  for Surl-Limits-2).
- Where a row's requirement also covers PUB, HPUB, SUB or UNSUB (NATS-ERR-2, NATS-CONNECT-9) or
  `natss` (NATS-INFO-1), name the BL-525 tests and leave `BL-526` or `BL-527` for the rest.

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/Nats.md` contains no `BL-525`.
- [ ] Each row that said `BL-525` names at least one existing test method as
      `ClassName.MethodName`, and each named test exists in
      `Surl.Extended.Protocol.Nats.UnitTests`.

## Notes

## Log

- 2026-11-05: Created.
