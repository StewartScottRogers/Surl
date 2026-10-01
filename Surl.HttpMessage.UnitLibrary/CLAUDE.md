# Surl.HttpMessage.UnitLibrary

Phase 4.

The HTTP/1.x message machinery the HTTP, WebSocket and RTSP servers share (ADR-0070), moved here
from `Surl.Protocol.Http.UnitLibrary` by BL-293 (ADR-0070 decision 2):

- `HttpMessageProtocol` - the protocol name and highest minor version a reader accepts and a
  response head writes: `Http11` (`HTTP`, 1) and `Rtsp10` (`RTSP`, 0).
- `HttpConnectionReader` - the request head read bounded by `ExchangeLimits.MaxRequestHeadBytes`,
  with the head timeout in `ReadNextRequestHeadAsync` (ADR-0070 decision 3). Its two-argument
  constructor reads `Http11`; the three-argument one reads the protocol it is given.
- `HttpRequestHead`, `HttpRequestField`, `HttpRequestHeadReadResult`,
  `HttpRequestHeadReadOutcome`, `HttpStatus`, `HttpResponseHead` (with `AddChallengeFields`,
  one `WWW-Authenticate` field per value), `HttpRequestBodyFraming` and
  `HttpRequestBodyFramingKind` - public, because three servers use them. `HttpRequestHead` and
  `HttpResponseHead` default to `Http11` when no protocol is given.
- `HttpRequestHeadLineReader`, `HttpRequestLineParser`, `HttpFieldLineParser` and `HttpSyntax` -
  internal, visible to `Surl.HttpMessage.UnitTests` only.

Which status answers which `HttpRequestHeadReadOutcome` (`400`, `431`, `408`, `505`) is each
server's, not this library's (ADR-0070 decision 4).

Namespace `Surl.HttpMessage`. This library references `Surl.Protocol.Abstractions.UnitLibrary`
and nothing else (ADR-0070 decision 1, amending ADR-0002's table); it never references
`Surl.Content`, `Surl.LineProtocol`, `Surl.Networking` or a protocol server. Protocol servers
may reference it; `Surl.Protocol.Http` does, and `Surl.Protocol.Ws` (BL-301) and
`Surl.Protocol.Rtsp` (BL-313) are to.

It reads and writes only through the `IConnection` the listener seam hands a server, with the
exchange's cancellation token, and times with `ExchangeContext.TimeProvider`. Never construct a
`Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`, and never touch the disk.

`Surl.HttpMessage.UnitTests` reads the request heads pinned upstream curl sent from
`Surl.Protocol.Http.UnitTests/Fixtures/`, embedded by link, so the bytes are recorded once.
