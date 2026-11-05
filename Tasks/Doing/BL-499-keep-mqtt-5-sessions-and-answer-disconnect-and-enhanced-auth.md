---
id: BL-499
title: Keep MQTT 5 sessions and answer DISCONNECT and enhanced AUTH
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-498, BL-416]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Mqtt5.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-499 — Keep MQTT 5 sessions and answer DISCONNECT and enhanced AUTH

## Goal

The MQTT 5 broker keeps a session across connections for its Session Expiry Interval, queues QoS 1 and 2 messages
for it, and resumes it with Session Present. It answers DISCONNECT with each reason code both ways. It runs
enhanced authentication and re-authentication with the AUTH packet for the methods BL-494's ADR offers.

## Context

- Decision: BL-494's ADR, decisions 2, 3 and 6.
- MQTT 5.0:
  - 4.1 (session state);
  - 3.1.2.4 (Clean Start) and 3.1.2.11.2 (Session Expiry Interval, and changing it on DISCONNECT, `0x82` if it
    was zero);
  - 3.14 (DISCONNECT, its reason codes, and `0x04` Disconnect with Will Message);
  - 3.15 and 4.12 (AUTH and enhanced authentication: Authentication Method and Data, `0x18` Continue
    authentication, `0x19` Re-authenticate, and `0x8C` Bad authentication method);
  - 4.13 (the server's DISCONNECT before closing on a protocol error).
- SCRAM exchanges: `Surl.Authentication` (BL-416), reached through the SASL contracts in `Surl.Protocol.Abstractions`.
- Time from `ExchangeContext.TimeProvider`.

## Acceptance criteria

- [ ] Named tests show:
      - a session resumed with Session Present 1 and its queued messages delivered;
      - Clean Start discarding it;
      - a session expiring after its interval;
      - DISCONNECT `0x04` publishing the Will;
      - DISCONNECT `0x00` not publishing it;
      - a DISCONNECT raising a zero Session Expiry Interval treated as a Protocol Error;
      - a SCRAM-SHA-256 enhanced authentication completed with `0x18` steps;
      - re-authentication with `0x19`;
      - an unknown method gets `0x8C`;
      - a protocol error answered with the server's DISCONNECT and its reason code.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
