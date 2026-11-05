---
id: BL-411
title: Accept extended CONNECT and HTTP datagrams in the HTTP/3 library
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-410, BL-573, BL-753]
touches: [Protocols/Http/Surl.Http3.UnitLibrary, Protocols/Http/Surl.Http3.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-411 — Accept extended CONNECT and HTTP datagrams in the HTTP/3 library

## Goal

A server built on the HTTP/3 library can do four things: advertise `SETTINGS_ENABLE_CONNECT_PROTOCOL` and
`SETTINGS_H3_DATAGRAM`, accept an extended CONNECT whose `:protocol` it handles, exchange HTTP datagrams and
capsules for that request stream, and claim the WebTransport stream types. This is what the WebTransport server
needs.

## Context

- Decision: BL-402's ADR (extended CONNECT, datagrams, WebTransport stream hand-off).
- Specifications: RFC 9220 (Bootstrapping WebSockets with HTTP/3, which defines extended CONNECT for HTTP/3 by
  reference to RFC 8441); RFC 9297 (HTTP datagrams: the quarter stream ID, `SETTINGS_H3_DATAGRAM` 0x33, the
  Capsule Protocol); RFC 9221 (the QUIC DATAGRAM frame and the `max_datagram_frame_size` transport parameter,
  which `Surl.Quic` must send); and draft-ietf-webtrans-http3-16, section 4 (the bidirectional stream signal
  `0x41` and unidirectional stream type `0x54`, each followed by the session ID).
- The HTTP server itself does not enable extended CONNECT. Its `SETTINGS` bytes do not change unless the ADR says so.
- If `Surl.Quic` lacks the DATAGRAM frame, add it in a separate task filed through `task-planner` and depend on it.
  Do not widen this one.

## Acceptance criteria

- [x] Named tests, each with bytes written from the RFC sections above:
      - a server that registers `:protocol` `webtransport` sends `SETTINGS` with `ENABLE_CONNECT_PROTOCOL = 1`
        and `H3_DATAGRAM = 1`;
      - an extended CONNECT for a registered protocol reaches its handler;
      - one for an unregistered protocol is answered as RFC 9220 says;
      - an HTTP datagram whose quarter stream ID names the request stream is delivered to it;
      - a capsule on the request stream is read with its type and length;
      - a `0x41` bidirectional stream and a `0x54` unidirectional stream are handed to the session they name.
- [x] The HTTP server's `SETTINGS` are unchanged: every `Surl.Protocol.Http.UnitTests` expected byte is unedited.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

What was built (Decided by Claude under Stewart's delegation; ADR-0086 decision 3):

- Tests, in `Surl.Http3.UnitTests`: `Http3ExtendedConnectTests.ServeAsync_HandlerRegistered_SendsSettingsWithEnableConnectProtocolAndH3DatagramThenFurtherSettings`
  (control stream `00 040E 0680100000 0801 3301 AC7CF00001`), `..._ExtendedConnectForARegisteredProtocol_ReachesItsHandler`,
  `..._ExtendedConnectForAnUnregisteredProtocol_Answers501WithFinThenStopsWithNoError`,
  `Http3ExtendedConnectSessionTests.ReceiveDatagramAsync_DatagramWhoseQuarterStreamIdNamesTheSession_ReturnsItsPayload`,
  `ReadCapsuleAsync_CapsuleSplitAcrossDataFrames_ReturnsItsTypeAndValueThenNullAtTheFin`,
  `AcceptStreamAsync_BidirectionalStreamWithTheSignal_IsHandedToTheSessionItNames` (`4041 00`) and
  `AcceptStreamAsync_UnidirectionalStreamOfTheType_IsHandedToTheSessionItNames` (`4054 00`), plus the error
  and edge cases. The task says `webtransport`; ADR-0117 pins the token `webtransport-h3`, which the tests use.
- Unregistered `:protocol`: `:status 501`, `server` when set, `content-length: 0`, FIN, then `STOP_SENDING`
  `H3_NO_ERROR`. RFC 9220 section 3: "the server SHOULD respond to the request with a 501 (Not Implemented)
  status code". No upstream curl byte is involved: curl does not send extended CONNECT over HTTP/3.
- `:protocol` is allowed only with a handler registered, only on `CONNECT` with `:scheme`, `:authority` and a
  non-empty `:path`; with none registered it stays an unknown pseudo-header (`H3_MESSAGE_ERROR`), so the HTTP
  server is unchanged. `SETTINGS`: `MAX_FIELD_SECTION_SIZE`, `0x08`=1, `0x33`=1, then `FurtherSettings`.
- Handler interface uses `ulong?` (QUIC varints) rather than ADR-0086's planning `long?`, and adds
  `ServeStreamWithoutSessionAsync`, so the WebTransport server applies ADR-0117 decision 3's own codes to a
  stream naming no open session; `Surl.Http3` knows no WebTransport code.
- Datagrams: quarter stream ID cut short or past 2^60 - 1 closes `H3_DATAGRAM_ERROR` (0x33); for no open
  session, dropped with a log note (RFC 9297 section 2.1 allows it). A session opens when its `CONNECT`
  arrives, so datagrams before the 2xx wait (128); those before the `CONNECT` are dropped.
- A session ID that is not a client bidirectional stream ID closes `H3_ID_ERROR`; a registered signal anywhere
  but a stream's first integer is `H3_FRAME_ERROR`, per ADR-0117 decision 3.
- Capsules: every type returned, the handler drops unknown ones; past `--max-message` resets
  `H3_EXCESSIVE_LOAD`; FIN inside a capsule resets `H3_MESSAGE_ERROR` (RFC 9297 section 3.3).
- `ReadClientSettingsAsync` exposes the client's `SETTINGS` so the WebTransport server can require
  `H3_DATAGRAM` and `WT_ENABLED` (ADR-0117 decision 0).
- `Surl.Quic` already had DATAGRAM frames (BL-573, BL-753): no Quic change, no task filed for it.
- Coverage: every new or changed line and branch is covered by `Surl.Http3.UnitTests` (cobertura from the
  MSTest collector). The lines it does not reach were already uncovered before this task and are driven by
  `Surl.Protocol.Http.UnitTests`, as BL-410 left them.
- ADR: `Documentation/Planning/Decisions` was held by BL-569 in Doing, so the amendment is filed as BL-830
  (docs, depends on BL-411) rather than written here; its body carries the nine decisions above.
- Fast tests: all green; `Surl.Http3.UnitTests` 82, stable over five runs; `Surl.Protocol.Http.UnitTests`
  459 green with no file edited.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Http3 serves extended CONNECT (RFC 9220), HTTP datagrams and capsules (RFC 9297) and hands 0x41/0x54 streams to their session; HTTP server bytes unchanged
