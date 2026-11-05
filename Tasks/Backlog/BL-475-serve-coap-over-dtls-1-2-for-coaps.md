---
id: BL-475
title: Serve CoAP over DTLS 1.2 for coaps
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-474, BL-415, BL-650]
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-475 — Serve CoAP over DTLS 1.2 for coaps

## Goal

The CoAP server serves `coaps` over `Surl.Dtls` in the modes BL-403's ADR chose, with PSK identities and
certificates from its caller. Every exchange, block-wise transfer and observation works as over `coap`, and a
peer that has not finished its handshake gets nothing.

## Context

- Decisions: BL-470's ADR, decision 6, and BL-403's ADR.
- `Surl.Dtls` (BL-412 to BL-415; add its reference, which its ADR-0002 row allows). RFC 7252 section
  9.1: one DTLS session per client endpoint, and CoAP messages as DTLS application data. Section 9.1.1: matching
  responses to requests within the same session, epoch included.
- PSK identity lookup is wired to the accounts through the `Surl.Protocol.Abstractions` contracts.

## Acceptance criteria

- [ ] Named tests, with a DTLS test client from `Surl.Dtls.UnitTests`' fixtures or written in this test project from
      RFC 6347, show:
      - a PSK handshake then a `GET` answered;
      - a certificate handshake then a block-wise `GET`;
      - an observation notified over DTLS;
      - a CoAP datagram before the handshake finishes ignored;
      - an unknown PSK identity refused as the DTLS ADR says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
