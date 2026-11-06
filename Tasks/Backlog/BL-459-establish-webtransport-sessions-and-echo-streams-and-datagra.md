---
id: BL-459
title: Establish WebTransport sessions and echo streams and datagrams over HTTP/3
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-458, BL-411, BL-748]
touches: [Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.WebTransport.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-459 — Establish WebTransport sessions and echo streams and datagrams over HTTP/3

## Goal

The WebTransport server registers `:protocol webtransport` with the HTTP/3 library and answers a valid extended
CONNECT with `200`. It then serves the session's bidirectional streams, unidirectional streams and datagrams as
BL-457's ADR decides, and ends the session with `WT_CLOSE_SESSION` or a drain.

## Context

- Decision: BL-457's ADR, decisions 1, 2, 3 and 6.
- The HTTP/3 library with extended CONNECT and datagrams: BL-411 (add its reference).
- WTH3: "Session Establishment", "WebTransport Features" (streams and datagrams), and "Session Termination".
  An unknown session ID on a stream or datagram is handled as WTH3 says (buffered for a while, or reset with
  `WT_BUFFERED_STREAM_REJECTED`).

## Acceptance criteria

- [ ] Named tests, over the in-memory QUIC test connection, show:
      - a CONNECT to an accepted path answered `200`;
      - one to another path refused as the ADR says;
      - a bidirectional stream's bytes echoed;
      - a unidirectional stream answered;
      - a datagram echoed with its quarter stream ID;
      - `WT_CLOSE_SESSION` from the client closing every stream of the session;
      - shutdown sending `WT_DRAIN_SESSION` and then `GOAWAY`;
      - a stream naming an unknown session handled as WTH3 says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
