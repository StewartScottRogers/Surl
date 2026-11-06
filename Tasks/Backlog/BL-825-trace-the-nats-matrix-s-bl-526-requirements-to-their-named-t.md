---
id: BL-825
title: Trace the NATS matrix's BL-526 requirements to their named tests
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-526]
touches: [Documentation/Conformance/Extended/Nats.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-825 — Trace the NATS matrix's BL-526 requirements to their named tests

## Goal

Every row of `Documentation/Conformance/Extended/Nats.md` that names BL-526 names the test that
proves it instead.

## Context

- BL-526 built PUB, HPUB, SUB, UNSUB, MSG and HMSG on the shared bus. Its tests are in
  `Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests`, mostly
  `NatsServerConnectionTests.Routing.cs`, plus `NatsControlLineReaderTests`,
  `NatsHeaderBlockTests` and `NatsSubjectsTests`.
- BL-526 could not edit the matrix: `Documentation/Conformance` was in BL-395's `touches` while
  it ran. ADR-0113 says each task replaces its ID with the named test.
- Rows to trace: NATS-PROTO-4, NATS-PUB-1, NATS-PUB-2, NATS-SUB-1, NATS-UNSUB-1, NATS-UNSUB-2,
  NATS-MSG-1, NATS-ECHO-1, Subject-1 to Subject-3, Queue-1, Surl-Bus-1 to Surl-Bus-3,
  Headers-1 to Headers-5, Surl-Limits-1.

## Acceptance criteria

- [ ] `Nats.md` names no BL-526; each of the rows above names one or more existing test methods
      in `Surl.Extended.Protocol.Nats.UnitTests` that prove it.

## Notes

## Log

- 2026-11-05: Created.
