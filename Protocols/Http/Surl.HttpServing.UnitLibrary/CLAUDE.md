# Surl.HttpServing.UnitLibrary

Answering HTTP/1.1 requests on one connection, shared by the HTTP server and the extended servers
that answer HTTP/1.1 (S3, AzureStorage, GoogleCloudStorage, OciRegistry, McpServer, SignalR,
SocketIo, DnsQuery, Mqtt5), so none of them references `Surl.Protocol.Http` (ADR-0084, decision 1;
built by BL-407). Namespace `Surl.HttpServing`.

A server implements `IHttpRequestHandler` - decide an `HttpAnswer` for an `HttpRequest`, never
write it, never ask which HTTP version carried it (only a handler deciding an upgrade reads
`HttpRequest.Http1Version`, ADR-0091) - and its `ServeAsync` awaits
`new Http1ConnectionServer(handler, context).ServeAsync(connection)`.

Public, because a server uses them:

- `Http1ConnectionServer` - the loop: reads each head through `Surl.HttpMessage`'s
  `HttpConnectionReader` (head timeout, `--max-request-head`), refuses what RFC 9112 calls
  malformed (`505`, `431`, `408`, `400`, a missing or repeated `Host`, an invalid
  `Content-Length`) with `Date`, `Server: surl`, `Content-Length: 0` and `Connection: close`
  before the handler sees it, answers the rest through the handler, and keeps or closes.
- `IHttpRequestHandler`, `HttpRequest`, `HttpAnswer`, `HttpAnswerBody` (none, bytes, or a range
  of a content-store file; a `ContentFile` names the `ContentStore` it is copied from),
  `IHttpRequestBody` (`DiscardAsync`, optionally hashing, and `CopyToAsync`, which keeps the
  body), `HttpRequestBodyDiscardOutcome`.
- `IHttpHeadNotReadRefuser` - a handler that also implements it words the refusal of a head that
  could not be read (`505`, `431`, `408`, `400`), given the outcome and the standard refusal; the
  answer it returns is written as a refusal with `Connection: close`, its body only when
  `HttpAnswerBody.Bytes` (BL-720; the S3 server's `400 RequestHeaderSectionTooLarge`). A handler
  that does not implement it gets the standard refusal, byte for byte as before.
- `IHttpConnectionTakeover` - an `HttpAnswer.Takeover` is awaited after its `101` head is
  written, with the connection and its reader; nothing is drained or written after it.

Internal, visible to `Surl.HttpServing.UnitTests` only: `Http1RequestResponder`,
`Http1RequestBody` (`100 Continue` before the first read, ADR-0027), `HttpRequestBodyDiscarder`
(`Content-Length` and chunked, the upload limit, the chunk-line limit), `Http1AnswerWriter`
(status line, `Connection`, body, the one-second refusal write deadline, half-close),
`HttpConnectionPersistence` (RFC 9112 section 9.3), `HttpUnreadRequestDrainer` (the lingering
close, ADR-0024) and `ConnectionWriteStream`.

This library references `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.HttpMessage.UnitLibrary`
and `Surl.Content.UnitLibrary`, and nothing else (`ProtocolIsolationTests.HorizontalLibraries`).
It never references a protocol server, and never constructs a `Socket`, `TcpListener`,
`UdpClient`, `SslStream` or `HttpListener`: it reads and writes only through the `IConnection`
it is handed, and times with `ExchangeContext.TimeProvider`.

The HTTP server's bytes are pinned by the recorded fixtures in `Surl.Protocol.Http.UnitTests`;
the bytes in `Surl.HttpServing.UnitTests` are written from RFC 9112 sections 6, 7 and 9 and
RFC 9110 section 10.1.1, through a handler that knows nothing of HTTP framing.
