# Surl.Http3.UnitLibrary

Phase 6.

An HTTP/3 connection on one `Surl.Quic` connection (RFC 9114), hand-built because the base class
library has no public HTTP/3 server framing (ADR-0082), and moved here from `Surl.Protocol.Http`
so the extended servers can share it (ADR-0086 decision 2, BL-410). It holds the frames, the
control and QPACK streams, `SETTINGS`, `GOAWAY` and the ADR-0006 limits of ADR-0082 decision 7,
and hands each request stream to a handler.

Namespace `Surl.Http3`. This library references `Surl.Protocol.Abstractions.UnitLibrary`,
`Surl.Quic.UnitLibrary` and `Surl.Qpack.UnitLibrary` and nothing else (ADR-0086 decision 5;
`ProtocolIsolationTests` holds the row). `Surl.Protocol.Http` and `Surl.Extended.Protocol.WebTransport` reference it.

Public, what a server uses:

- `Http3ConnectionServer` - an `IQuicApplicationProtocol`, constructed with an
  `IHttp3RequestStreamHandler`, an `Http3ServerOptions` and the `ExchangeContext`. It opens the
  server's control stream (`SETTINGS` with `SETTINGS_MAX_FIELD_SECTION_SIZE` from
  `--max-message`) and QPACK streams, enforces the client's critical streams' rules, reads each
  request stream's `HEADERS` within the head timeout, answers a field section past
  `--max-message` `:status 431` itself, resets a malformed request `H3_MESSAGE_ERROR`, and calls
  the handler once per well-formed request on a task of its own. A handler that returns with the
  request body still arriving is followed by `STOP_SENDING` `H3_NO_ERROR`. On cancellation it
  sends `GOAWAY` and closes `H3_NO_ERROR`.
- `Http3ServerOptions` - `ServerFieldValue`, the `server` field of the `431` and `501` the
  connection writes itself (the HTTP server sets `surl`; null leaves it out);
  `ExtendedConnectHandlers` (BL-411, ADR-0086 decision 3), empty by default; and
  `FurtherSettings`, identifier and value pairs sent after the derived `SETTINGS`, empty by
  default. The HTTP server sets neither list, so its bytes are unchanged.
- `IHttp3ExtendedConnectHandler` - one `:protocol` (RFC 9220), with an optional
  `BidirectionalStreamSignal` and `UnidirectionalStreamType` (WebTransport's `0x41` and `0x54`),
  `ServeSessionAsync` and `ServeStreamWithoutSessionAsync`. With one registered: `SETTINGS` add
  `ENABLE_CONNECT_PROTOCOL` (0x08) = 1 and `H3_DATAGRAM` (0x33) = 1 before `FurtherSettings`;
  `:protocol` is accepted on a `CONNECT` with `:scheme`, `:authority` and a non-empty `:path`
  (otherwise `H3_MESSAGE_ERROR`; with none registered `:protocol` stays an unknown pseudo-header);
  an unregistered `:protocol` is answered `:status 501`, `content-length: 0`, FIN, then
  `STOP_SENDING` `H3_NO_ERROR`; each QUIC `DATAGRAM` is read as a quarter stream ID and payload
  (cut short or past 2^60 - 1 closes `H3_DATAGRAM_ERROR`, one for no open session is dropped with
  a log note); a stream opening with a registered signal (only as its first integer; later it is
  `H3_FRAME_ERROR`, also as a frame type after `HEADERS`) or of a registered unidirectional type is
  read past its session ID and handed to that handler's open session, else to its
  `ServeStreamWithoutSessionAsync`; a session ID that is not a client bidirectional stream ID
  closes `H3_ID_ERROR`. The composing server must also set `Surl.Quic`'s `MaxDatagramFrameSize`.
- `Http3ExtendedConnectSession` - open from the `CONNECT`'s arrival until `ServeSessionAsync`
  returns: `Id`, `Head` (`Protocol` set), `RequestStream` (to answer and end it),
  `ReadClientSettingsAsync`, `ReadCapsuleAsync` (every type returned, the handler drops unknown
  ones; past `--max-message` resets `H3_EXCESSIVE_LOAD`, a FIN inside one `H3_MESSAGE_ERROR`),
  `WriteCapsuleAsync` (one `DATA` frame), `ReceiveDatagramAsync` (128 wait, oldest dropped),
  `SendDatagramAsync` (quarter stream ID prefixed), `AcceptStreamAsync` and
  `OpenUnidirectionalStreamAsync` (type and session ID written first).
- `Http3SessionStream` - a claimed stream past its header: `Id`, `SessionId`, `Stream`,
  `ReadAsync`, `WriteAsync`, `ResetAsync`, `ResetAtAsync`, `StopSendingAsync`. `Http3Capsule` -
  type and value, with `DatagramType` 0x00.
- `IHttp3RequestStreamHandler` and `Http3RequestStream` - a stream as its handler sees it: `Id`,
  `Head` (an `Http3RequestHead`, mapped and validated by `Http3RequestHeadMapping`, this library's
  copy of `Surl.Http2`'s rules), `IsRequestBodyAnnounced`, `ReadBodyAsync` (the `DATA` pieces as
  asked for, unknown frames skipped and trailers dropped, with `HasContentLengthMismatch` and
  `HasRequestBodyEndedEarly` once it ends), `WriteHeadersAsync`, `WriteDataAsync` (one `DATA`
  frame; empty with `endStream` sends the `FIN` alone), `CreateResponseBodyStream` (each write one
  `DATA` frame), `ResetAsync` (`RESET_STREAM` and `STOP_SENDING`, a stream error) and
  `ResetResponseAsync` (`RESET_STREAM` alone, for an answer that cannot be finished).
- `Http3ErrorCode`, `Http3RequestHead`, `Http3RequestHeadMapping`.

Internal: `Http3RequestStreamServer`, `Http3StreamReader` (throws `Http3ConnectionException` for a
rule that ends the connection, `Http3StreamAbandonedException` for a stream the client reset),
`Http3FieldSections` (QPACK with no dynamic table), `Http3ExtendedConnect` (one connection's
registrations, open sessions and the client's `SETTINGS`), `Http3FrameWriter`, `Http3FrameType`,
`Http3FrameHeader`, `Http3FrameLimit`, `Http3Settings`, `Http3DataStream` and
`Http3HeadTimeoutDelay`. They are visible to `Surl.Http3.UnitTests`, to
`Surl.Protocol.Http.UnitTests`, which drives the connection through the HTTP server, and to
`Surl.Extended.Protocol.WebTransport.UnitTests`, which drives it through the WebTransport server.

`Surl.Http3.UnitTests` owns the in-memory HTTP/3 test client (`Http3TestQuicClient`,
`Http3TestResponse`, `UnitTestQuicClient`, `UnitTestTls13Client`), which starts from pinned
upstream curl's recorded Initials (`Fixtures/h3-client-initial`, embedded from
`Surl.Protocol.Http.UnitTests`) and which `Surl.Protocol.Http.UnitTests` and
`Surl.Extended.Protocol.WebTransport.UnitTests` compile in by link. Its
own tests drive the connection with handlers of their own, never the Curl port (ADR-0003).

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener` here: the
QUIC connection arrives from `Surl.Quic`'s `QuicDatagramFlowServer` over the listener seam.
