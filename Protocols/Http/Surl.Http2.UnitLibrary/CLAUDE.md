# Surl.Http2.UnitLibrary

Phase 6.

An HTTP/2 connection on one `IConnection` (RFC 9113), hand-built because the base class library
has no public HTTP/2 server framing (ADR-0081 decision 4), and moved here from
`Surl.Protocol.Http` so the extended servers can share it (ADR-0086 decision 1, BL-409). It holds
the frames, streams, flow control, `SETTINGS`, `PING`, `RST_STREAM`, `GOAWAY` and the ADR-0006
limits of ADR-0081 decision 5, and hands each request stream to a handler.

Namespace `Surl.Http2`. This library references `Surl.Protocol.Abstractions.UnitLibrary` and
`Surl.Hpack.UnitLibrary` and nothing else (ADR-0086 decision 5; `ProtocolIsolationTests` holds
the row). `Surl.Protocol.Http` references it, and gRPC will.

Public, what a server uses:

- `Http2ConnectionServer` - the connection loop: constructed with an `IHttp2StreamHandler`, the
  `ExchangeContext` and the `IConnection` positioned after the client preface; `ServeAsync()`
  serves it, `ServeUpgradedAsync(Http2RequestHead, settings)` serves one upgraded from HTTP/1.1
  with `Upgrade: h2c`, the upgraded request on stream 1. It answers `PING`, applies the client's
  `SETTINGS`, enforces the limits, and calls the handler once per validated request stream on a
  task of its own. A handler that returns while the client's half is still open is followed by
  `RST_STREAM` `NO_ERROR`.
- `IHttp2StreamHandler` and `Http2RequestStream` - a stream as its handler sees it: `Head` (an
  `Http2RequestHead`, mapped and validated by `Http2RequestHeadMapping`), `ReadBodyAsync` (the
  `DATA`, each piece's window given back when the next is asked for, with
  `HasContentLengthMismatch` and `HasRequestBodyEndedEarly` once it ends), `WriteHeadersAsync`,
  `WriteDataAsync`, `CreateResponseBodyStream` (an `Http2DataStream` that ends the stream on
  the body's last byte), `WriteTrailersAsync` (a `HEADERS` with `END_STREAM` after the `DATA`,
  RFC 9113 section 8.1) and `ResetAsync`. Reading and writing are independent.
- `Http2ConnectionPreface` (tells the 24 preface bytes from an HTTP/1.x head),
  `Http2SettingsPayload` (decodes an `HTTP2-Settings` payload), `Http2Setting`,
  `Http2SettingIdentifier`, `Http2ErrorCode`, and `Http2RequestHeadMapping`; `Surl.Http3` keeps
  its own copy of the mapping for HTTP/3, `Http3RequestHeadMapping`, since it may not reference this library.

Internal: the frame codec (`Http2Frame` and its records, `Http2FrameEncoder`, `Http2FrameReader`,
`Http2FrameParser`, `Http2FrameMalformations`, `Http2FrameWriter`, which owns the HPACK encoder),
`Http2Stream`, `Http2RequestBody`, `Http2SendWindows`, `Http2ConnectionError` and
`Http2HeadTimeoutDelay`. They are visible to `Surl.Http2.UnitTests` and to
`Surl.Protocol.Http.UnitTests`, which drives the connection through the HTTP server with bytes
recorded from pinned upstream curl and encodes and parses frames with this codec to do it.

`Surl.Http2.UnitTests` owns the HTTP/2 test client (`Http2TestClient`, `Http2TestResponse`,
`Http2TestBytes`, `Http2TestStream`), which `Surl.Protocol.Http.UnitTests` compiles in by link.
Its own tests replay bytes from RFC 9113 and from pinned upstream curl's recording, never from the
Curl port (ADR-0003).

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener` here: the
connection arrives through the listener seam in `Surl.Protocol.Abstractions`.
