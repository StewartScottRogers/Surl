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
`Surl.Http2.UnitLibrary` and `Surl.Http3.UnitLibrary` for the HTTP/2 and HTTP/3 connections (ADR-0086), `Surl.Hpack.UnitLibrary` for the
header fields HTTP/2 carries, and `Surl.Quic.UnitLibrary` and `Surl.Qpack.UnitLibrary` for
HTTP/3 (ADR-0081, ADR-0082). Method dispatch, content responses and the authentication flow stay here.

Answering is split from HTTP/1.x framing (ADR-0081, decision 8). `HttpRequestAnswerer`,
the server's `IHttpRequestHandler`, decides each answer - an `HttpAnswer` of status, fields, `HttpAnswerBody` and whether it is
a refusal - from an `HttpRequest` whose `IHttpRequestBody` it may read or leave unread, and
never asks which HTTP version carried it. The HTTP/1.x side is `Surl.HttpServing`'s
`Http1ConnectionServer`, which `HttpProtocolServer.ServeAsync` constructs with the answerer
for each connection. Keep version
questions out of the answerer: an HTTP/2 or HTTP/3 writer reuses it as it is.

The answerer maps each `GET` and `HEAD` with `ContentStore.MapRequestPathForCaller`: the caller is
the account the verdict's `AccountName` names (an empty or absent one is anonymous) on the exchange's
`Scheme`. A share the caller may not read answers as a missing path, the same empty `404` (ADR-0102
decisions 2.2, 2.3).

A `GET`'s `Range` is read by `HttpRangeSelection` (ADR-0133 decision 1): one satisfiable `bytes`
range is answered `206` with `Content-Range`, one naming no byte `416` with
`Content-Range: bytes */<length>`, and several ranges, other units, bad syntax or an `If-Range` that
is not the exact `Last-Modified` get the whole file `200`. Under `--allow-uploads` a `PUT` is
answered by `HttpRequestAnswerer.Uploads.cs` (ADR-0133 decision 2), mapped for the same caller: the
body is copied through `HttpUploadStream` into a content-store upload session, hashed when a login
binds the body, and committed only once all of it arrived and the body check accepted it - `201` new,
`204` replaced, `403` not permitted, `409` a directory or no such directory, `413` past the limit.
Without `--allow-uploads` a `PUT` is the `405` every unserved method is.

A fault rule's chosen error (ADR-0134 decision 1) is answered by `HttpRequestAnswerer.Faults.cs`: the
plan is asked through `Surl.HttpMessage`'s `HttpCommandFaults` once a request is let in (a body-bound
`PUT` once its body check accepts), before the upload limit and the content store; the answer is the
empty answer a `404` is with the reply's status, or a refusal when a body was announced and not read.

The HTTP/2 frames, streams, flow control and connection (RFC 9113; ADR-0081 decisions 4 to 7;
ADR-0090) live in `Surl.Http2.UnitLibrary` (ADR-0086 decision 1, moved by BL-409); its `CLAUDE.md`
describes them. This library keeps what is HTTP-server specific: `HttpProtocolServer.ServeAsync` reads the first bytes of a cleartext connection, or of a TLS
connection whose `TlsSession.ApplicationProtocol` is `h2` (a TLS connection that agreed
`http/1.1` or no ALPN goes straight to `Http1ConnectionServer`; ADR-0081 decision 2), with
`Http2ConnectionPreface` and hands it over on the whole preface, or gives the bytes back
through `AlreadyReadBytesConnection` to `Http1ConnectionServer`, whose handler is
`Http2CleartextUpgradeHandler` on a cleartext connection (over TLS it is the answerer itself,
since `h2c` is cleartext only): a request `Http2CleartextUpgrade` accepts for `Upgrade: h2c`
is answered `101` with an `IHttpConnectionTakeover` that runs
`Http2ConnectionServer.ServeUpgradedAsync` over `HttpConnectionReaderConnection`, so the bytes
the HTTP/1.x reader already holds come first (ADR-0091). Either way the connection's
`IHttp2StreamHandler` is `Http2StreamAnswerWriter`: for each request stream it first resets
`HTTP_1_1_REQUIRED` a stream whose `authorization` names NTLM or Negotiate, unseen by the
answerer, so no connection-bound handshake starts (ADR-0081 decision 7; BL-602), then builds an
`HttpRequest` whose body is an `Http2HttpRequestBody` (the stream's `DATA` against the upload
limit, with the interim `:status 100` for `expect: 100-continue`), has the same
`HttpRequestAnswerer` decide the `HttpAnswer`, resets the stream `PROTOCOL_ERROR` when the body
did not add up to its `content-length`, resets it `HTTP_1_1_REQUIRED` when the answer is a `401`
whose every challenge is NTLM or Negotiate, and writes the answer as one header block and `DATA`
within the client's windows, resetting `INTERNAL_ERROR` when a content-store file cannot be sent
whole.

The HTTP/3 connection (RFC 9114; ADR-0082 decisions 7 to 9 and its Amendment 1) - frames, the
control and QPACK streams, `SETTINGS`, `GOAWAY` and request streams - lives in
`Surl.Http3.UnitLibrary` (ADR-0086 decision 2, moved by BL-410); its `CLAUDE.md` describes it.
`surl --http3` serves it through `Http3ProtocolServer` (BL-384), the `IDatagramProtocolServer` for
`https`'s UDP side: one `Surl.Quic` `QuicDatagramFlowServer` per flow with the server certificate,
`--idle-timeout` as QUIC's idle timeout and the head timeout as the handshake's, and
`Http3ApplicationProtocol`, which starts the connection's authentication session with the TLS 1.3
session QUIC agreed and serves the connection with `Surl.Http3`'s `Http3ConnectionServer`, its
`Http3ServerOptions` naming `server: surl` for the `431` the connection writes itself. Cancelling
the exchange before the handshake completes stops the flow at once; after it, the HTTP/3 server
answers it. With `--http3`, `HttpProtocolServer` wraps its answerer in `AltSvcAdvertisingHandler`
on every TLS connection, adding `Alt-Svc: h3=":<port>"; ma=86400`. The connection's
`IHttp3RequestStreamHandler` is `Http3StreamAnswerWriter`: for each request stream it builds an
`HttpRequest` whose body is an `Http3HttpRequestBody` (the stream's `DATA` against the upload
limit, with the interim `:status 100` for `expect: 100-continue`), has the same
`HttpRequestAnswerer` decide the `HttpAnswer`, resets the stream `H3_MESSAGE_ERROR` when the body
did not add up to its `content-length`, and writes the answer as `HEADERS` and `DATA` frames and
`FIN`, resetting the response `H3_INTERNAL_ERROR` when a content-store file cannot be sent whole.
Its tests drive it over the in-memory QUIC test connection, `Http3TestClient`, a subclass of
`Surl.Http3.UnitTests`' `Http3TestQuicClient` (compiled in by link), which starts from pinned
upstream curl's recorded Initials (`Fixtures/h3-client-initial`).

The upstream test cases' server half (ADR-0080 decision 2; BL-391) is a test seam the shipped
`surl` never composes: `Surl.Conformance.UnitTests` composes `HttpUpstreamTestCaseServer` with one
case's `HttpUpstreamTestCaseReplies` (its reply parts by name as bytes and its `<servercmd>`
lines). Before the first request, `HttpUpstreamTestCaseProxyHeader` reads past a PROXY protocol
version 1 header (`--haproxy-protocol`, `--haproxy-clientip`; BL-681), recorded as the first
received line; one with no CRLF within 107 bytes closes the connection with no bytes. The shipped
`HttpProtocolServer` does not read PROXY headers. The server reads each head with `Surl.HttpMessage`'s reader and its body as framed
(`HttpUpstreamTestCaseRequestBody`; no `100 Continue` is ever written), then sends the part
`HttpUpstreamTestCasePartNumber` picks verbatim: FILEFORMAT.md's `NUM` from the run of digits
right after the last `/` of the whole request target, query included, up to the first non-digit
(`/data/10240002.txt` gives part 2; for `CONNECT`, the digits the host name ends in) and the `Authorization` (else `Proxy-Authorization`)
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
