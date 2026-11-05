---
id: BL-511
title: Negotiate AMQP 1.0 protocol headers and SASL
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-510]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-511 — Negotiate AMQP 1.0 protocol headers and SASL

## Goal

The AMQP 1.0 container negotiates the protocol headers as BL-509's ADR decides, answering an unsupported
one with its own header and closing. It runs the SASL layer with `sasl-mechanisms`, `sasl-init` and `sasl-outcome`
for `PLAIN` (and `ANONYMOUS` when allowed), then expects the AMQP header.

## Context

- Decision: BL-509's ADR, decisions 1 and 2.
- AMQP 1.0:
  - Part 2, section 2.2 (version negotiation: answer with the supported header, then close);
  - Part 5, section 5.3 (the SASL layer: `sasl-mechanisms`, `sasl-init`, `sasl-challenge`/`sasl-response`,
    `sasl-outcome` codes 0 ok, 1 auth, 2 sys, 3 sys-perm, 4 sys-temp, and the AMQP header after a successful
    outcome).
  RFC 4616 for `PLAIN`, and RFC 4505 for `ANONYMOUS`.
- Logins go through the SASL contracts in `Surl.Protocol.Abstractions` (`PlainSaslExchange` in `Surl.Authentication`
  already answers `PLAIN`).

## Acceptance criteria

- [ ] Named tests show:
      - SASL with good `PLAIN` credentials completing with outcome 0 and then the AMQP header;
      - a bad password gets outcome 1 and is closed;
      - `ANONYMOUS` refused unless allowed;
      - the plain AMQP header without SASL answered with the SASL header and closed when anonymous is off;
      - an unsupported version answered with the supported header and closed;
      - a SASL frame after the outcome refused.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
