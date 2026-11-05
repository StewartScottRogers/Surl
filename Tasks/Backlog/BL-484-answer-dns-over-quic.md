---
id: BL-484
title: Answer DNS over QUIC
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-482, BL-379, BL-381, BL-572]
touches: [Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-484 — Answer DNS over QUIC

## Goal

The DNS server answers RFC 9250 DNS over QUIC on ALPN `doq`. Each query arrives on its own client-initiated
bidirectional stream with a two-byte length prefix, the DNS message ID must be 0, and protocol violations close
the connection with the RFC's error codes.

## Context

- Decision: BL-478's ADR, decisions 5 and 6, and BL-402's ADR (how a server rides `Surl.Quic` directly,
  without HTTP/3).
- `Surl.Quic` (BL-373 to BL-379), and the QUIC seam in `Surl.Protocol.Abstractions` (BL-380, BL-381). Add the
  reference that `Surl.Quic`'s ADR-0002 row allows.
- RFC 9250:
  - section 4.2 (one query per stream, the 2-byte length, the stream ended after the response, ID 0);
  - section 4.3 (error codes `DOQ_NO_ERROR` 0, `DOQ_INTERNAL_ERROR` 1, `DOQ_PROTOCOL_ERROR` 2,
    `DOQ_REQUEST_CANCELLED` 3, `DOQ_EXCESSIVE_LOAD` 4);
  - section 5.4 (no 0-RTT answers that change state);
  - section 4.1.1 (ALPN `doq`).

## Acceptance criteria

- [ ] Named tests over the in-memory QUIC test connection show:
      - a query on a stream answered on that stream, which is then closed;
      - two streams answered independently;
      - a query with a non-zero ID ends the connection with `DOQ_PROTOCOL_ERROR`;
      - a stream that sends no FIN after its query handled as the RFC says;
      - over-limit streams refused with `DOQ_EXCESSIVE_LOAD` or the ADR's choice.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
