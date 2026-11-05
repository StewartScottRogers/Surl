# Surl.Protocol.Http.UnitLibrary

Phase 1; HTTP/2 and HTTP/3 in Phase 6.

The HTTP server: HTTP/1.0 and HTTP/1.1 first, then hand-built HTTP/2 and HTTP/3. It serves
the content store and answers every method, body framing, range and conditional request,
redirect, content encoding and authentication challenge upstream curl can exercise.

**URL schemes answered:** `http`, `https`, and the `/ipfs/` and `/ipns/` gateway paths upstream curl turns `ipfs://` and `ipns://` URLs into

This library references `Surl.Protocol.Abstractions.UnitLibrary`, and may also reference
the horizontal libraries in ADR-0002 decision 3's table, as later ADRs amend it - nothing
else. Today it references `Surl.Content.UnitLibrary` (the content store) and
`Surl.HttpMessage.UnitLibrary` (ADR-0070): the request-head reader and its head timeout, the
request and response head types, `HttpStatus` and body framing live there; method dispatch,
content responses, the authentication flow, `100 Continue`, persistence, body discarding and
the lingering-close drain stay here.

Answering is split from HTTP/1.x framing (ADR-0081, decision 8). `HttpRequestAnswerer`
decides each answer - an `HttpAnswer` of status, fields, `HttpAnswerBody` and whether it is
a refusal - from an `HttpRequest` whose `IHttpRequestBody` it may read or leave unread, and
never asks which HTTP version carried it. The HTTP/1.x side is `Http1RequestResponder`
(refuses heads RFC 9112 calls malformed, builds the `HttpRequest`), `Http1RequestBody`
(`100 Continue`, then the body discarder) and `Http1AnswerWriter` (the status line,
`Connection`, the body, the refusal write deadline, half-close and drain). Keep version
questions out of the answerer: an HTTP/2 or HTTP/3 writer reuses it as it is.

Referencing another protocol server is a
build break, and `Surl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`
here. The server receives its transport from the listener seam in
`Surl.Protocol.Abstractions`, so the tests in the matching `.UnitTests` project can drive
it with request bytes measured from pinned upstream curl, with no network.

Expected bytes come from a build pinned in `UpstreamCurlBuilds.json`, measured with
`Record-CurlExchange.ps1`, and never from the Curl port (ADR-0003).
