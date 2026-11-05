---
id: BL-537
title: Negotiate XMPP STARTTLS and require it before authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-536]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-537 — Negotiate XMPP STARTTLS and require it before authentication

## Goal

On `xmpp` the XMPP server offers `<starttls><required/></starttls>`. It answers `<starttls/>` with `<proceed/>`
and upgrades the connection, then restarts the stream. Until TLS is negotiated it refuses anything else as RFC 6120
section 5 says, unless BL-535's ADR's explicit option allows plain text.

## Context

- Decision: BL-535's ADR, decision 2.
- RFC 6120 section 5.3 and 5.4 (the STARTTLS flow, `<proceed/>` and `<failure/>`, the stream restart without
  closing, and features not re-offering STARTTLS), and section 9.1 (the client-to-server examples of TLS).
- The TLS upgrade comes from the listener seam, as the mail servers' STARTTLS does (ADR-0010; `IsTlsUpgradeAvailable`,
  ADR-0069). The server never constructs an `SslStream`.

## Acceptance criteria

- [ ] Named tests, with the in-memory TLS upgrade the mail servers' tests use, show:
      - features offering required STARTTLS;
      - `<starttls/>` answered `<proceed/>` and then the upgrade;
      - the restarted stream's features offering SASL and not STARTTLS;
      - a SASL `<auth/>` before TLS refused as section 5 says;
      - `<starttls/>` without a configured certificate answered `<failure/>` and closed;
      - plain text allowed only under the ADR's option.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
