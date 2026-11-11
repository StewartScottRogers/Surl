---
id: BL-831
title: Trace the NATS TLS requirements in Nats.md to BL-527's tests
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-527]
touches: [Documentation/Conformance/Extended/Nats.md]
requirement: none
created: 2026-11-05
completed: 2026-11-06
---
# BL-831 — Trace the NATS TLS requirements in Nats.md to BL-527's tests

## Goal

`Documentation/Conformance/Extended/Nats.md` names the test that proves each of `Nats:NATS-TLS-1` to
`Nats:NATS-TLS-5` instead of `BL-527`, and states that a first byte within 50 ms starts the handshake.

## Context

- BL-527 built `natss` (ADR-0113 decision 6) but could not edit `Documentation/Conformance`, which BL-396 held at
  the time. ADR-0113's Consequences: "each task replaces its ID with the named test".
- The tests, in `Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests/NatsServerConnectionTests.Tls.cs`:
  - NATS-TLS-1: `ServeAsync_NatssInfoFirst_SendsInfoWithTlsRequiredThenUpgradesAndAcceptsConnectOverTls`;
  - NATS-TLS-2: `ServeAsync_NatssTlsFirst_UpgradesBeforeSendingInfoInsideTls` and
    `ServeAsync_NatssAnyByteWithinTheTlsFirstWait_StartsTheHandshakeBeforeInfo`;
  - NATS-TLS-3: `ServeAsync_NatssPlainConnectAfterInfo_IsSecureConnectionTlsRequiredAndClosed`;
  - NATS-TLS-4: `ServeAsync_NatsWithAClientHello_NeverOffersOrStartsTls` and
    `NatsInfoTests.Encode_IPv4_IsTheInfoLineWithTheAdrsFieldsInOrder`;
  - NATS-TLS-5: `ServeAsync_NatssHandshakeFails_ThrowsWithNoBytesWritten` and
    `ServeAsync_NatssHandshakeUnfinishedWithinTheHeadTimeout_ClosesWithNoBytes`.

## Acceptance criteria

- [x] `Nats.md` rows `Nats:NATS-TLS-1` to `Nats:NATS-TLS-5` each name the tests above, and no row names `BL-527`.

## Notes

- Filed by BL-527 (dark factory lane 8, 2026-11-05).

## Log

- 2026-11-05: Created.
- 2026-11-06: Backlog -> Doing.
- 2026-11-06: Doing -> Done. Nats.md rows NATS-TLS-1 to NATS-TLS-5 name the tests that prove natss; no row names BL-527
