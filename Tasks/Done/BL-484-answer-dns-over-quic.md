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
completed: 2026-11-05
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

- [x] Named tests over the in-memory QUIC test connection show:
      - a query on a stream answered on that stream, which is then closed;
      - two streams answered independently;
      - a query with a non-zero ID ends the connection with `DOQ_PROTOCOL_ERROR`;
      - a stream that sends no FIN after its query handled as the RFC says;
      - over-limit streams refused with `DOQ_EXCESSIVE_LOAD` or the ADR's choice.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

Delivered (dark factory lane 6, 2026-11-05):

- `DnsOverQuicConnectionServer` (`IQuicApplicationProtocol`) answers each client bidirectional
  stream on its own task; `DnsOverQuicProtocolServer` (`IDatagramProtocolServer`, scheme `dns`)
  runs it on `QuicDatagramFlowServer` with ADR-0092 decision 5's options (ALPN `doq` only, bidi
  100, uni 0, no DATAGRAM frames, the idle timeout, the head timeout as handshake timeout);
  `DnsOverQuicErrorCode` holds RFC 9250 section 4.3's codes. The library now references
  `Surl.Quic.UnitLibrary`, as ADR-0086's "who references `Surl.Quic`" names.
- Tests (31 new, `DnsOverQuicConnectionServerTests`, `DnsOverQuicProtocolServerTests`) run over
  `DnsOverQuicTestClient`: pinned upstream curl's recorded HTTP/3 Initials (embedded from
  `Surl.Protocol.Http.UnitTests/Fixtures`, as `Surl.Http3.UnitTests` does) with the ALPN list
  edited from `h3`, `h3-29` to `doq`, `h3-29`. The edit grows the ALPN extension by one byte and
  keeps the ClientHello's length by folding the optional `version_information` parameter into a
  reserved one a byte shorter. `UnitTestQuicClient` and `UnitTestTls13Client` are copies of
  `Surl.Http3.UnitTests`' (test code, not a verdict).
- Library coverage: 100% line, 100% branch (Measure-CodeQuality.ps1), worst CRAP 10.

Choices within ADR-0092, taken as the sensible defaults:

- A message the answerer drops (shorter than a header, or QR = 1) closes the connection with
  `DOQ_PROTOCOL_ERROR`: over DoQ a stream must be answered or the connection failed, and RFC 9250
  section 4.3.3 makes a malformed stream a protocol error. A message too short to hold an ID is
  dropped by that rule rather than checked for ID 0.
- Bytes after the query are found by reading on after the answer has been written with FIN, so the
  query is answered and the connection then closed with `DOQ_PROTOCOL_ERROR`.
- A stream with no FIN after its query is answered and its receiving half left open until the
  connection ends (section 4.2 lets the server answer first); not an error.
- A stream reset by the client, or cut off by the connection ending, before its query is whole is
  dropped with a log note and no answer (section 4.3.1). A FIN before the message is whole is
  `DOQ_PROTOCOL_ERROR`.
- Over-limit streams: ADR-0092's choice, `Surl.Quic`'s transport `STREAM_LIMIT_ERROR` (0x04) for a
  stream past `initial_max_streams_bidi` 100; pinned by
  `ServeAsync_StreamPastTheHundredAllowed_IsRefusedByQuicWithStreamLimitError`.
- When the exchange is cancelled (shutdown, idle timeout, maximum duration) the server returns and
  the flow loop closes with application code 0, `DOQ_NO_ERROR`; before the handshake completes the
  flow stops at once, as `Http3ProtocolServer` does.

Left for other tasks: EDNS(0) padding on DoQ is BL-826; the connection-limit refusal with
`DOQ_EXCESSIVE_LOAD` (an `IDatagramRefusalWriter`) is filed as BL-832; composing the server behind
`--dns-transport quic` is BL-485.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The DNS server answers DNS over QUIC (RFC 9250) on ALPN doq, one query per stream, with DOQ_PROTOCOL_ERROR for violations
