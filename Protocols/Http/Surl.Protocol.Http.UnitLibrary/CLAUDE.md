# Surl.Protocol.Http.UnitLibrary

Phase 1; HTTP/2 and HTTP/3 in Phase 6.

The HTTP server: HTTP/1.0 and HTTP/1.1 first, then hand-built HTTP/2 and HTTP/3. It serves
the content store and answers every method, body framing, range and conditional request,
redirect, content encoding and authentication challenge upstream curl can exercise.

**URL schemes answered:** `http`, `https`, and the `/ipfs/` and `/ipns/` gateway paths upstream curl turns `ipfs://` and `ipns://` URLs into

This library references `Surl.Protocol.Abstractions.UnitLibrary`, and may also reference
the horizontal libraries in ADR-0002 decision 3's table, as later ADRs amend it - nothing
else. Today it references `Surl.Content.UnitLibrary` (the content store),
`Surl.HttpMessage.UnitLibrary` (ADR-0070: the request-head reader and its head timeout, the
request and response head types, `HttpStatus` and body framing) and `Surl.HttpServing.UnitLibrary`
(ADR-0084, decision 1: `HttpRequest`, `HttpAnswer`, request bodies and `100 Continue`, response
writing, persistence, the lingering-close drain and the `Http1ConnectionServer` loop), with
`Surl.Hpack.UnitLibrary` for HTTP/2 and `Surl.Quic.UnitLibrary` and `Surl.Qpack.UnitLibrary` for
HTTP/3 (ADR-0081, ADR-0082). Method dispatch, content responses and the authentication flow stay here.

Answering is split from HTTP/1.x framing (ADR-0081, decision 8). `HttpRequestAnswerer`,
the server's `IHttpRequestHandler`, decides each answer - an `HttpAnswer` of status, fields, `HttpAnswerBody` and whether it is
a refusal - from an `HttpRequest` whose `IHttpRequestBody` it may read or leave unread, and
never asks which HTTP version carried it. The HTTP/1.x side is `Surl.HttpServing`'s
`Http1ConnectionServer`, which `HttpProtocolServer.ServeAsync` constructs with the answerer
for each connection. Keep version
questions out of the answerer: an HTTP/2 or HTTP/3 writer reuses it as it is.

HTTP/2 frames (RFC 9113 sections 4 and 6; ADR-0081 decision 4) are typed values: one
`Http2Frame` record per frame type (`Http2DataFrame` ... `Http2ContinuationFrame`), each
writing its own payload, with `Http2FrameEncoder` adding the 9-byte `Http2FrameHeader`.
`Http2FrameReader` reads them bounded by the advertised `SETTINGS_MAX_FRAME_SIZE` - an
oversized frame is refused from its header alone and an unknown type is skipped without
being held - and `Http2FrameParser` checks what one frame can get wrong on its own, each
`Http2FrameMalformation` mapping to the RFC 9113 error code and connection-or-stream scope
in `Http2FrameMalformations`. Rules that need connection or stream state (`CONTINUATION`
order, a `HEADERS` priority naming its own stream, a client's `PUSH_PROMISE`) belong to the
connection, not the codec.

The HTTP/2 connection (ADR-0081, decisions 3, 5 to 7; ADR-0090) is `Http2ConnectionServer`:
`HttpProtocolServer.ServeAsync` reads the first bytes of a cleartext connection, or of a TLS
connection whose `TlsSession.ApplicationProtocol` is `h2` (a TLS connection that agreed
`http/1.1` or no ALPN goes straight to `Http1ConnectionServer`; ADR-0081 decision 2), with
`Http2ConnectionPreface` and hands it over on the whole preface, or gives the bytes back
through `AlreadyReadBytesConnection` to `Http1ConnectionServer`, whose handler is
`Http2CleartextUpgradeHandler` on a cleartext connection (over TLS it is the answerer itself,
since `h2c` is cleartext only): a request `Http2CleartextUpgrade` accepts for `Upgrade: h2c`
is answered `101` with an `IHttpConnectionTakeover` that runs
`Http2ConnectionServer.ServeUpgradedAsync` over `HttpConnectionReaderConnection`, so the bytes
the HTTP/1.x reader already holds come first (ADR-0091). The connection reads frames in
order, decodes header blocks with `Surl.Hpack`'s decoder, maps each request with
`Http2RequestHeadMapping` to an `HttpRequest` whose body is an `Http2RequestBody`, and answers
each `Http2Stream` on a task of its own through the same `HttpRequestAnswerer`;
`Http2StreamAnswerWriter` turns the `HttpAnswer` into frames, `Http2FrameWriter` serialises
every frame and owns the HPACK encoder, and `Http2SendWindows` and `Http2DataStream` keep `DATA`
within the client's windows. This library references `Surl.Hpack.UnitLibrary` for it, and
BL-409 moves the connection to `Surl.Http2.UnitLibrary` (ADR-0086).

HTTP/3 (RFC 9114; ADR-0082 decisions 7 to 9 and its Amendment 1) is `Http3ConnectionServer`, the
`IQuicApplicationProtocol` a `Surl.Quic` `QuicDatagramFlowServer` hands a connection to once its
handshake agreed `h3`; this library references `Surl.Quic.UnitLibrary` and `Surl.Qpack.UnitLibrary`
for it, and BL-410 moves it to `Surl.Http3.UnitLibrary` (ADR-0086). Nothing composes it yet: BL-384
serves it from `surl`. It opens its control stream (`SETTINGS` from `Http3FrameWriter`) and its QPACK
encoder and decoder streams, accepts the client's (the control stream's rules, and
`Http3Settings`, close the connection with the RFC's codes), and serves each request stream on a
task of its own with `Http3RequestStreamServer`: `HEADERS` within the head timeout, decoded by
`Http3FieldSections` (QPACK with no dynamic table) and mapped by `Http2RequestHeadMapping`, then
the same `HttpRequestAnswerer`, its `HttpAnswer` written as `HEADERS` and `DATA` frames and `FIN`.
`Http3RequestBody` reads `DATA` from the stream as the handler asks; `Http3StreamReader` reads
frames, throwing `Http3ConnectionException` for a rule that ends the connection and
`Http3StreamAbandonedException` for a stream the client reset. On cancellation (shutdown,
`--max-time`) the server sends `GOAWAY`, cuts the open streams off and closes `H3_NO_ERROR`. Its
tests drive it over the in-memory QUIC test connection, `Http3TestClient`, which starts from pinned
upstream curl's recorded Initials (`Fixtures/h3-client-initial`).

The upstream test cases' server half (ADR-0080 decision 2; BL-391) is a test seam the shipped
`surl` never composes: `Surl.Conformance.UnitTests` composes `HttpUpstreamTestCaseServer` with one
case's `HttpUpstreamTestCaseReplies` (its reply parts by name as bytes and its `<servercmd>`
lines). The server reads each head with `Surl.HttpMessage`'s reader and its body as framed
(`HttpUpstreamTestCaseRequestBody`; no `100 Continue` is ever written), then sends the part
`HttpUpstreamTestCasePartNumber` picks verbatim: FILEFORMAT.md's `NUM` from the trailing digits
of the path (the host for `CONNECT`) and the `Authorization` (else `Proxy-Authorization`)
scheme, `swsbounce` moving the case's next request to the next part. The connection stays open
unless `swsclose` is in the part or the commands; a missing part closes it with no bytes.
`HttpUpstreamTestCaseServerCommands` holds `auth_required`, `idle`, `no-expect`, `skip:`,
`swsclose`, `writedelay:` (a pause on the exchange's `TimeProvider` between the reply's lines)
and `Testnum`; any other command throws `NotSupportedException`, as the selector skips such a
case first. `ReceivedBytesRecordingConnection` records every byte read, in arrival order, as
`HttpUpstreamTestCaseReplies.ReceivedBytes` for the case's `<protocol>`.

Referencing another protocol server is a
build break, and `Surl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`
here. The server receives its transport from the listener seam in
`Surl.Protocol.Abstractions`, so the tests in the matching `.UnitTests` project can drive
it with request bytes measured from pinned upstream curl, with no network.

Expected bytes come from a build pinned in `UpstreamCurlBuilds.json`, measured with
`Record-CurlExchange.ps1`, and never from the Curl port (ADR-0003).
