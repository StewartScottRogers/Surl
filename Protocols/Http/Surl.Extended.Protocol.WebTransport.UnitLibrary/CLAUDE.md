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
  constructed with the listen URL's path, the `IAuthenticationPolicy` and the `ExchangeContext`. It
  runs an `Http3ConnectionServer` that registers `webtransport-h3` (signal `0x41`, stream type `0x54`)
  and sends `SETTINGS_WT_ENABLED` = 1 and the three `SETTINGS_WT_INITIAL_*`. The composing server (BL-461) sets `Surl.Quic`'s `MaxDatagramFrameSize`
  to its `MaxDatagramFrameSize` (65535) and `ResetStreamAt`.
- `WebTransportConnectHandler` - waits for the client's `SETTINGS` within `--head-timeout` (else
  resets the CONNECT `H3_REQUEST_INCOMPLETE`), requires `SETTINGS_H3_DATAGRAM` = 1 and
  `SETTINGS_WT_ENABLED` = 1 (else closes the connection `WT_REQUIREMENTS_NOT_MET`, BL-895), judges it by
  `WebTransportConnectJudgement`'s rules (`400` for a `:scheme` other than `https`, `403` for another origin, then the login, then `405` with
  `allow: CONNECT` for a path outside the listen path, a query ignored) and the session limit, and
  answers `200` and serves the session.
- `WebTransportRequestStreamHandler` - every other request: `405`, `allow: CONNECT`, FIN.
- `WebTransportSession` - the echo: a bidirectional stream's bytes back on it, a unidirectional
  stream's on a server unidirectional stream of the session (`H3_REQUEST_REJECTED` `STOP_SENDING`
  when the client allows none), HTTP datagrams and `DATAGRAM` capsules back unchanged (dropped when
  the client cannot take them). Client resets and `STOP_SENDING` are mirrored with their code. The
  client's `WT_CLOSE_SESSION`, FIN or reset of the CONNECT stream ends every stream of the session
  `WT_SESSION_GONE`, then FIN (a reset for a reset); a malformed capsule or one after
  `WT_CLOSE_SESSION` resets the CONNECT stream `H3_MESSAGE_ERROR`; `WT_DRAIN_SESSION`, flow-control
  and unknown capsules are ignored. Shutdown (BL-895; `WebTransportApplicationProtocol.ShutdownGrace`, 5 seconds, set as
  `Http3ServerOptions.ShutdownGrace`) sends `WT_DRAIN_SESSION` through `AddShutdownWrite` before
  `Surl.Http3`'s `GOAWAY`, keeps echoing through the grace, then sends `WT_CLOSE_SESSION` code 0
  `server shutting down` and FIN. Datagrams that arrive before their CONNECT are held by `Surl.Http3`
  and echoed once the session is accepted.
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

Flow control, the session limit, authentication and the limits (BL-460, ADR-0117 decisions 4, 5, 7 and 8):

- `WebTransportApplicationProtocol` is also constructed with the `IAuthenticationPolicy`; each
  connection starts its `IHttpAuthenticationSession` as TLS 1.3, and `WebTransportConnectAuthentication`
  judges every CONNECT after the origin and before the path: `Proceed` goes on, `Challenge` is `401`
  with the verdict's challenges, `Forbidden` `403`; NTLM and Negotiate credentials are never judged
  and are answered `401` with the challenges the request earns without them, neither scheme offered.
  `--allow-anonymous` is the policy's to apply.
- The server sends `SETTINGS_WT_INITIAL_MAX_STREAMS_UNI` and `_BIDI` = 100 and `_MAX_DATA` = 1048576
  (`WebTransportFlowControlSettings`; tests pass smaller ones through the internal constructor).
  Flow control is on when the client sends any of them non-zero. The session limit is 1 without it
  and 16 with it, counted by `WebTransportSessionDirectory.TryOpenSession`; the next CONNECT is reset
  `H3_REQUEST_REJECTED`.
- With flow control on, `WebTransportFlowControl` holds the client to `WebTransportReceiveLimits`
  (streams each way and payload read; past either ends the session `WT_FLOW_CONTROL_ERROR`, raised
  limits sent as `WT_MAX_STREAMS` and `WT_MAX_DATA`, never past `--max-filesize`) and the echo to the
  client's limits through `WebTransportSendCredit` (`WT_DATA_BLOCKED` and `WT_STREAMS_BLOCKED` once per
  limit value). A client `WT_MAX_*` that does not raise its last, or a `WT_MAX_STREAMS` above 2^60, is a
  `WT_FLOW_CONTROL_ERROR`; `WT_MAX_STREAM_DATA` and `WT_STREAM_DATA_BLOCKED` are `H3_MESSAGE_ERROR`.
- Whatever the flow control, a session whose stream payload and HTTP datagrams reach `--max-filesize`
  is closed `WT_CLOSE_SESSION` code 1 `session data limit reached`, then FIN.
- `WebTransportDatagramEchoQueue` (BL-905, decision 8) holds at most 64 HTTP datagrams a session waiting for their echo and drops the newest past that, noting it in the log.

Still to come: waiting for the client to close each
CONNECT stream before `CONNECTION_CLOSE` at shutdown (BL-1060).

`Surl.Extended.Protocol.WebTransport.UnitTests` drives the server over `Surl.Http3.UnitTests`' in-memory
HTTP/3 test client, compiled in by link, with `UnitTestWebTransport` writing the client's side.
