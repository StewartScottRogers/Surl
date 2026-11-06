# Surl.Extended.Protocol.WebTransport.UnitLibrary

An extended server (ADR-0083): it answers the `webtransport` scheme the Curl port adds, which
upstream curl does not have. ADR-0117 decides how. It is judged by WebTransport over HTTP/3,
draft-ietf-webtrans-http3-16 (WTH3), RFC 9297, RFC 9220 and RFC 9221, never by the Curl port's
output, and its unit tests replay bytes written by hand from those specifications' layouts.

It references `Surl.Protocol.Abstractions`, `Surl.Quic` (for `QuicVariableLengthInteger`, RFC 9000
section 16's codec, reused rather than copied) and `Surl.Http3` (extended CONNECT, HTTP datagrams and
the streams that name a session, BL-411), and no other protocol server.

The server (BL-459, ADR-0117 decisions 0 to 3 and 6):

- `WebTransportApplicationProtocol` - the public `IQuicApplicationProtocol` for one QUIC connection,
  constructed with the listen URL's path and the `ExchangeContext`. It runs an `Http3ConnectionServer`
  that registers `webtransport-h3` (signal `0x41`, stream type `0x54`) and sends
  `SETTINGS_WT_ENABLED` = 1. The composing server (BL-461) sets `Surl.Quic`'s `MaxDatagramFrameSize`
  to its `MaxDatagramFrameSize` (65535) and `ResetStreamAt`.
- `WebTransportConnectHandler` - waits for the client's `SETTINGS` within `--head-timeout` (else
  resets the CONNECT `H3_REQUEST_INCOMPLETE`), requires `SETTINGS_H3_DATAGRAM` = 1 and
  `SETTINGS_WT_ENABLED` = 1 (else resets it `WT_REQUIREMENTS_NOT_MET`), judges it with
  `WebTransportConnectJudgement` (`403` for another origin, `405` with `allow: CONNECT` for a path
  outside the listen path, a query ignored), and answers `200` and serves the session.
- `WebTransportRequestStreamHandler` - every other request: `405`, `allow: CONNECT`, FIN.
- `WebTransportSession` - the echo: a bidirectional stream's bytes back on it, a unidirectional
  stream's on a server unidirectional stream of the session (`H3_REQUEST_REJECTED` `STOP_SENDING`
  when the client allows none), HTTP datagrams and `DATAGRAM` capsules back unchanged (dropped when
  the client cannot take them). Client resets and `STOP_SENDING` are mirrored with their code. The
  client's `WT_CLOSE_SESSION`, FIN or reset of the CONNECT stream ends every stream of the session
  `WT_SESSION_GONE`, then FIN (a reset for a reset); a malformed capsule or one after
  `WT_CLOSE_SESSION` resets the CONNECT stream `H3_MESSAGE_ERROR`; `WT_DRAIN_SESSION`, flow-control
  and unknown capsules are ignored. Shutdown sends `WT_DRAIN_SESSION`, `WT_CLOSE_SESSION` code 0
  `server shutting down` and FIN, before `Surl.Http3`'s `GOAWAY`.
- `WebTransportSessionDirectory` - one connection's sessions as the streams naming them see them
  (WTH3 4.6): a stream for a session not answered yet is held (16 at once, `--head-timeout`) and
  served once it is accepted; refused, timed out, past the 16 or naming an ordinary request it gets
  `WT_BUFFERED_STREAM_REJECTED`, and naming an ended session `WT_SESSION_GONE`.
- `WebTransportHttp3Codes` - the settings and error codepoints above.

The codecs (BL-458, ADR-0117 decisions 4 and 6), with no transport:

- Capsules (RFC 9297 section 3.2): `WebTransportCapsuleType` names the codepoints;
  `WebTransportCloseSessionCapsule` (32-bit code, UTF-8 message of at most 1024 bytes, refused past
  that), `WebTransportDrainSessionCapsule`, `WebTransportDatagramCapsule`, `WebTransportLimitCapsule`
  (the six flow-control capsules carrying one variable-length integer) and `WebTransportUnparsedCapsule`
  (unknown types, and `WT_MAX_STREAM_DATA` and `WT_STREAM_DATA_BLOCKED`, which HTTP/3 prohibits).
  `WebTransportCapsuleWriter` writes one; `WebTransportCapsuleReader.Read` reads one and says
  `Incomplete` while it has not all arrived and `Malformed` when its value breaks its layout, and
  `TryReadHeader` gives the declared length before the value arrives.
- Stream headers (WTH3 4.2, 4.3): `WebTransportStreamHeader` writes and reads `0x41` or `0x54` and the
  session ID.
- `WebTransportApplicationErrorCode` maps 32-bit application codes into `WT_APPLICATION_ERROR`'s
  HTTP/3 range and back (WTH3 4.4).

Still to come: flow control, the session limit, authentication and the limits (BL-460); closing the
connection for unmet requirements, the `:scheme` check, datagrams held before their CONNECT and
serving through the shutdown grace, once `Surl.Http3` offers them (BL-888, BL-889).

`Surl.Extended.Protocol.WebTransport.UnitTests` drives the server over `Surl.Http3.UnitTests`' in-memory
HTTP/3 test client, compiled in by link, with `UnitTestWebTransport` writing the client's side.
