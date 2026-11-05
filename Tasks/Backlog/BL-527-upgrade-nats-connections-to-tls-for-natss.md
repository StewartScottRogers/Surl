---
id: BL-527
title: Upgrade NATS connections to TLS for natss
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-526]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-527 — Upgrade NATS connections to TLS for natss

## Goal

For `natss`, the NATS server sends INFO with `tls_required: true` and then upgrades the connection to TLS before
reading CONNECT, as the NATS protocol does. It also accepts a TLS-first handshake if BL-524's ADR decides
so, and refuses plain-text CONNECT where TLS is required.

## Context

- Decision: BL-524's ADR, decision 6.
- The TLS upgrade comes from the listener seam, as the mail servers' STARTTLS does (ADR-0010: how a protocol
  server upgrades a connection; `IsTlsUpgradeAvailable`, ADR-0069). The server never constructs an `SslStream`.
- The NATS page: INFO's `tls_required`, then the client starts TLS. `-ERR 'Secure Connection - TLS Required'`
  for a client that sends CONNECT in plain text.

## Acceptance criteria

- [ ] Named tests, with the in-memory TLS upgrade the mail servers' tests use, show:
      - INFO with `tls_required` followed by an upgrade and then CONNECT accepted over TLS;
      - plain-text CONNECT after `tls_required` refused with the page's `-ERR`;
      - TLS-first accepted or refused as the ADR says;
      - `nats` (no TLS) never offering TLS when no certificate is configured.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
