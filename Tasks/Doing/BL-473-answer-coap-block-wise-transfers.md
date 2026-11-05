---
id: BL-473
title: Answer CoAP block-wise transfers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-472]
touches: [Protocols/Http/Surl.Extended.Protocol.Coap.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Coap.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-473 — Answer CoAP block-wise transfers

## Goal

The CoAP server sends large representations in Block2 blocks of the size the client asks for (or the ADR's
preferred size), with Size2. It receives large uploads in Block1 blocks with `2.31 Continue`, refusing
incomplete or oversized ones as RFC 7959 says.

## Context

- Decision: BL-470's ADR, decisions 4 and 7.
- RFC 7959: section 2.2 (the block option's NUM, M and SZX), 2.3 and 2.4 (Block2 in responses, and the client's
  size preference), 2.5 (Block1, `2.31 Continue`, `4.08 Request Entity Incomplete`, `4.13` with Size1), and 4
  (Size1 and Size2). Section 3's figures are the expected exchanges for tests.

## Acceptance criteria

- [ ] Named tests replay RFC 7959 section 3's figures, served from a content-store file of the figure's size: a
      simple Block2 `GET`, a `GET` with an early size negotiation, a Block1 `PUT` with `2.31`, and a `PUT` whose
      block is out of order getting `4.08`. A Block1 total over the limit gets `4.13` with Size1, and a `GET` with
      `Size2: 0` gets Size2 back.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
