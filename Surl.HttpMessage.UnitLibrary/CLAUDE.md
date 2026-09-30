# Surl.HttpMessage.UnitLibrary

Phase 4.

The HTTP/1.x message machinery the HTTP, WebSocket and RTSP servers share (ADR-0070). The
project exists and holds no code yet: BL-293 moves it here from `Surl.Protocol.Http.UnitLibrary`.
What it is to hold, as intent until BL-293 lands (ADR-0070 decision 2):

- `HttpMessageProtocol` - the protocol name and highest minor version a reader accepts and a
  response head writes: `Http11` (`HTTP`, 1) and `Rtsp10` (`RTSP`, 0).
- `HttpConnectionReader` - the request head read bounded by `ExchangeLimits.MaxRequestHeadBytes`,
  with the head timeout in `ReadNextRequestHeadAsync` (ADR-0070 decision 3).
- `HttpRequestHead`, `HttpRequestField`, `HttpRequestHeadReadResult`,
  `HttpRequestHeadReadOutcome`, `HttpStatus`, `HttpResponseHead`, `HttpRequestBodyFraming` and
  `HttpRequestBodyFramingKind` - public, because three servers use them.
- `HttpRequestHeadLineReader`, `HttpRequestLineParser`, `HttpFieldLineParser` and `HttpSyntax` -
  internal, visible to `Surl.HttpMessage.UnitTests` only.

Namespace `Surl.HttpMessage`. This library references `Surl.Protocol.Abstractions.UnitLibrary`
and nothing else (ADR-0070 decision 1, amending ADR-0002's table); it never references
`Surl.Content`, `Surl.LineProtocol`, `Surl.Networking` or a protocol server. Protocol servers
may reference it; `Surl.Protocol.Http` (BL-293), `Surl.Protocol.Ws` (BL-301) and
`Surl.Protocol.Rtsp` (BL-313) are to. No server references it yet.

It is to read and write only through the `IConnection` the listener seam hands a server, with
the exchange's cancellation token, and time with `ExchangeContext.TimeProvider`. Never
construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`, and never
touch the disk.
