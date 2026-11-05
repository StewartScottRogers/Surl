# ADR-0084 — The libraries the extended servers share for HTTP/1.1, WebSocket and DTLS

- **Status:** Accepted
- **Amended by:** [ADR-0086](ADR-0086-how-the-extended-servers-share-http-2-http-3-and-quic.md)
  (2026-11-04, BL-402), which decides what this ADR left to BL-402: the HTTP/2 connection moves
  to `Surl.Http2` and the HTTP/3 connection to `Surl.Http3`, and an extended server answering
  HTTP/2 or HTTP/3 implements their stream handlers rather than `IHttpRequestHandler`.
- **Amended by:** [ADR-0087](ADR-0087-how-surls-dtls-1-2-server-handshakes-for-coaps.md)
  (2026-11-04, BL-403): decision 3's `Surl.Dtls` row may also reference
  `Surl.Cryptography.Curve25519.UnitLibrary`, for the x25519 group; it needs no ChaCha20 or
  Poly1305.
- **Date:** 2026-11-04
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04,
  in BL-401. BL-407 builds `Surl.HttpServing` (decision 1), BL-408 builds `Surl.WebSocket`
  (decision 2), and BL-412 to BL-415 build `Surl.Dtls` (decision 3) to BL-403's handshake ADR.
- **Amends:** [ADR-0002](ADR-0002-mirror-the-curl-ports-project-map.md) decision 3: its table of
  horizontal libraries gains the three rows of decisions 1, 2 and 3. Everything else in ADR-0002
  stands, and so does [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)'s
  isolation rule, which this ADR serves.

## Context

ADR-0083 lets an extended server (`Surl.Extended.Protocol.*`) reference only
`Surl.Protocol.Abstractions` and the horizontal libraries of ADR-0002's table, never an original
protocol server. Several extended servers need machinery that today lives inside an original one:

- **HTTP/1.1 serving.** S3, AzureStorage, GoogleCloudStorage, OciRegistry and McpServer's
  Streamable HTTP answer HTTP/1.1 requests; so do SignalR's negotiate request, DnsQuery's DNS over
  HTTPS, and the upgrade request under SignalR, SocketIo and Mqtt5's `mqtt5+ws`.
  `Surl.HttpMessage` (ADR-0070) already holds the bounded head reader, request-line and field
  parsing, `HttpResponseHead`, `HttpStatus` and `HttpRequestBodyFraming`. Request bodies
  (`Content-Length` and chunked, `100 Continue`), response writing (`ConnectionWriteStream`),
  keep-alive (`HttpConnectionPersistence`), draining (`HttpRequestBodyDiscarder`,
  `HttpUnreadRequestDrainer`) and the lingering close (in `HttpProtocolServer`) are still in
  `Protocols/Http/Surl.Protocol.Http.UnitLibrary`. BL-366 splits that server's answering from its
  HTTP/1.x framing into the shape [ADR-0081](ADR-0081-how-surl-serves-http-2.md) decision 8
  names: `HttpRequestAnswerer`, which decides an `HttpAnswer` for an `HttpRequest` and never
  writes, and `Http1AnswerWriter`, which writes HTTP/1.x bytes.
- **WebSocket.** SignalR, SocketIo and `mqtt5+ws` speak RFC 6455. The opening-handshake checks,
  framing, reassembly, close codes and lingering close are in
  `Protocols/Http/Surl.Protocol.Ws.UnitLibrary` ([ADR-0071](ADR-0071-how-the-websocket-server-answers-upstream-curl.md)).
- **DTLS 1.2.** `coaps` runs CoAP over DTLS 1.2 (RFC 7252 section 9.1; RFC 6347). The base class
  library has no DTLS, and `SslStream` cannot run over datagrams, so it is built by hand.
- **Logins.** XMPP needs SCRAM-SHA-1 and SCRAM-SHA-256 (RFC 5802, RFC 7677); Kafka, AMQP, STOMP,
  NATS, Redis and MQTT 5 have their own login steps.
- **Object stores.** S3, AzureStorage, GoogleCloudStorage and OciRegistry each store blobs.

HTTP/2 and HTTP/3 sharing (gRPC, WebTransport, DNS over QUIC) is BL-402's decision, not this one's.

This ADR pins no byte any client sends or expects. The original servers' bytes do not change (each
building task's acceptance says so), and every extended server's own ADR cites the specification
sections its bytes come from, as ADR-0083 decision 1 requires.

## Decision

### 1. HTTP/1.1 serving: a new `Surl.HttpServing.UnitLibrary`

| Horizontal library | Folder | May itself reference | Holds |
| --- | --- | --- | --- |
| `Surl.HttpServing.UnitLibrary` | `Protocols/Http/` | `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.HttpMessage.UnitLibrary`, `Surl.Content.UnitLibrary` | answering HTTP/1.1 requests on one connection: the request and answer types, request bodies, response writing, keep-alive, draining and the lingering close |

- **A new library, not more of `Surl.HttpMessage`.** `Surl.HttpMessage` is message *syntax*, and
  RTSP shares it (ADR-0070). What moves here is HTTP/1.1's connection *semantics*: chunked bodies,
  `100 Continue`, `Connection: close` and keep-alive, drain-before-close. RTSP has none of these
  (RFC 2326 has no chunked bodies, and its sessions outlive connections), so putting them in
  `Surl.HttpMessage` would hand the RTSP server code it must never call. Namespace
  `Surl.HttpServing`.
- **Why `Surl.Content`.** ADR-0081 decision 8's `HttpAnswerBody` has three kinds: none, bytes, or a
  content-store file with its length and range. Keeping that kind as it is decided needs
  `ContentStore`, `ContentPathMapping` and `ContentByteRange`; `Surl.Content` is already a
  horizontal library referencing only Abstractions, and the object-store servers use it anyway
  (decision 5).
- **What moves, after BL-366, type by type.** Names unchanged (ADR-0070's rule: one concept, one
  name). `public` only what a server uses; the rest `internal` with `InternalsVisibleTo` the
  library's own tests.

  | Type | From | Visibility |
  | --- | --- | --- |
  | `HttpRequest` (method, target, authority, fields, body) | BL-366, `Surl.Protocol.Http` | public |
  | `IHttpRequestBody` and its HTTP/1.x implementation (`Content-Length` and chunked reading, `100 Continue` sent before the first read, ADR-0027) | BL-366, `Surl.Protocol.Http` | interface public, implementation internal |
  | `HttpAnswer`, `HttpAnswerBody` | BL-366, `Surl.Protocol.Http` | public |
  | `Http1AnswerWriter` (writes an `HttpAnswer` as HTTP/1.x bytes; owns `Connection: close`, keep-alive and chunked framing) | BL-366, `Surl.Protocol.Http` | internal |
  | `ConnectionWriteStream` | `Surl.Protocol.Http` | internal |
  | `HttpConnectionPersistence` | `Surl.Protocol.Http` | internal |
  | `HttpRequestBodyDiscarder`, `HttpRequestBodyDiscardOutcome` | `Surl.Protocol.Http` | internal |
  | `HttpUnreadRequestDrainer` (drain-before-close, ADR-0024) | `Surl.Protocol.Http` | internal |
  | the lingering close of ADR-0021, today in `HttpProtocolServer` | `Surl.Protocol.Http` | internal |
  | `IHttpRequestHandler` (new; below) | - | public |
  | `Http1ConnectionServer` (new; below) | `HttpProtocolServer`'s head-read, answer and keep-alive loop | public |

- **What stays in `Surl.Protocol.Http`:** `HttpRequestAnswerer` (content store lookups, ranges,
  listings and uploads), its authentication judging, `HttpProtocolServer`
  (which composes the pieces, and tells HTTP/2's preface from an HTTP/1.x head, ADR-0081), and every
  HTTP/2 and HTTP/3 type until BL-402 decides otherwise.
- **The one request-handler shape an extended HTTP server implements:**

  ```csharp
  public interface IHttpRequestHandler
  {
      Task<HttpAnswer> AnswerAsync(HttpRequest request, CancellationToken cancellationToken);
  }
  ```

  The handler decides, never writes, and never asks which HTTP version it answers - ADR-0081
  decision 8's rule for `HttpRequestAnswerer`, which implements it. It may read the request's body
  through `IHttpRequestBody` or leave it; an unread body is drained or the connection closed by
  `Http1ConnectionServer`, exactly as the HTTP server does today. An extended HTTP server's
  `IConnectionProtocolServer.ServeAsync` constructs an `Http1ConnectionServer` with its handler and
  `ExchangeContext`, and awaits `ServeAsync(IConnection)`: the loop reads each head through
  `Surl.HttpMessage`'s `HttpConnectionReader` (with its head timeout and `--max-request-head`
  bound), answers it through the handler, writes it through `Http1AnswerWriter`, keeps or closes
  the connection, and lingers on close. Logins go through `IHttpAuthenticationSession`, which the
  server receives as the HTTP server does; S3's AWS Signature Version 4 is already one of its
  schemes (ADR-0045).
- **An upgrade hands the connection over.** For SignalR, SocketIo and `mqtt5+ws`, an `HttpAnswer`
  may carry a connection takeover (an `IHttpConnectionTakeover`, given the `IConnection` and the
  `HttpConnectionReader` with any bytes it has already read). `Http1ConnectionServer` writes the
  answer's head - a `101 Switching Protocols` - then awaits the takeover, which owns the connection
  until it ends; nothing is drained or written after it. The HTTP server's own answerer never
  returns one, so its bytes do not change.
- **Who references it:** `Surl.Protocol.Http` (BL-407 moves the types), and every extended server
  that answers HTTP/1.1: S3, AzureStorage, GoogleCloudStorage, OciRegistry, McpServer, SignalR,
  SocketIo, DnsQuery and Mqtt5. `Surl.Protocol.Ws` and `Surl.Protocol.Rtsp` do not change.
- **Built by BL-407,** which depends on BL-366. BL-407 creates the projects beside
  `Surl.HttpMessage` in `Protocols/Http/`, lists them in `Surl.slnx` under `/Protocols/Http/`, and
  adds the row to `ProtocolIsolationTests.HorizontalLibraries`.

### 2. WebSocket: a new `Surl.WebSocket.UnitLibrary`

| Horizontal library | Folder | May itself reference | Holds |
| --- | --- | --- | --- |
| `Surl.WebSocket.UnitLibrary` | `Protocols/Http/` | `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.HttpMessage.UnitLibrary` | RFC 6455's opening-handshake checks and `101` answer, framing, message reassembly, close codes and the lingering close |

- The planning name is adopted. Namespace `Surl.WebSocket`. It does not reference
  `Surl.HttpServing`: the WebSocket server reads its upgrade head with `Surl.HttpMessage` alone
  (ADR-0071), and an extended server reaches the library from `Surl.HttpServing`'s takeover with
  the same `HttpConnectionReader`.
- **What moves, type by type,** from `Protocols/Http/Surl.Protocol.Ws.UnitLibrary`, names unchanged:

  | Type | Holds |
  | --- | --- |
  | `WebSocketAcceptKey` | `Sec-WebSocket-Accept` from `Sec-WebSocket-Key` (RFC 6455 section 4.2.2) |
  | `WebSocketUpgradeChecks`, `WebSocketUpgradeRefusal`, `WebSocketHttpStatuses` | the method and field checks of an upgrade request, and the refusal each failure gets |
  | `WebSocketFrame`, `WebSocketFrameReader`, `WebSocketFrameReadOutcome`, `WebSocketFrameReadResult`, `WebSocketFrameEncoder` | reading masked client frames and writing unmasked server frames (section 5) |
  | `WebSocketOpcode`, `WebSocketOpcodes` | the opcodes |
  | `WebSocketMessage`, `WebSocketMessageReassembler`, `WebSocketReassemblyOutcome`, `WebSocketReassemblyStep` | fragmented messages and their UTF-8 check |
  | `WebSocketCloseCodes` | the close status codes (section 7.4) |
  | `WebSocketLingeringClose` | the wait for the client's close after the server's |

  A type a server uses is `public`; the rest `internal` with `InternalsVisibleTo` the library's
  own tests.
- **What stays in `Surl.Protocol.Ws`:** `WebSocketUpgradeResponder` (ADR-0071 decision 1's check
  order, its content store and its authentication session), `WebSocketServedEntry`,
  `WebSocketExchange` and `WsProtocolServer`. After BL-408 the responder builds its `101` through the library
  (below) with no subprotocol, so the WebSocket server's bytes do not change, and ADR-0071's "no
  subprotocol and no extension is ever selected" stands for `ws` and `wss`.
- **How a server names its subprotocol.** The library gains one public type,
  `WebSocketUpgradeAcceptance`: given the request head and the subprotocols the server speaks, in
  its own preference order (empty for the WebSocket server, `["mqtt"]` for `mqtt5+ws`, MQTT 5.0
  chapter 6), it builds the `101 Switching Protocols` `HttpResponseHead` with `Upgrade`,
  `Connection` and `Sec-WebSocket-Accept`, and with `Sec-WebSocket-Protocol` naming the first of
  the server's subprotocols the client's `Sec-WebSocket-Protocol` lists (RFC 6455 section 4.2.2:
  exactly one of the client's values, or none). It reports which it chose, or none. Whether a
  server refuses an upgrade that offers none of its subprotocols is that server's own ADR's
  decision, answered with a `WebSocketUpgradeRefusal`; the library never refuses on it.
  No extension (`permessage-deflate`) is negotiated by the library.
- **Who references it:** `Surl.Protocol.Ws`, and the extended servers SignalR, SocketIo and Mqtt5.
- **Built by BL-408,** which creates the projects in `Protocols/Http/`, lists them under
  `/Protocols/Http/` in `Surl.slnx`, and adds the row to `ProtocolIsolationTests.HorizontalLibraries`.

### 3. DTLS 1.2: a new `Surl.Dtls.UnitLibrary`

| Horizontal library | Folder | May itself reference | Holds |
| --- | --- | --- | --- |
| `Surl.Dtls.UnitLibrary` | `Cryptography/` | `Surl.Protocol.Abstractions.UnitLibrary` | the server side of DTLS 1.2 (RFC 6347 over RFC 5246): record layer, handshake messages and their fragmentation, the TLS 1.2 PRF, the handshake, alerts |

- The planning name is adopted. Namespace `Surl.Dtls`.
- **`Cryptography/`, not `Foundation/`.** ADR-0079 decision 3 puts a hand-built primitive in
  `Cryptography/` and a service every server may use in `Foundation/`. DTLS is hand-built security
  machinery that one server uses, the same kind of thing as `Surl.Tls13`, which ADR-0082 put in
  `Cryptography/`.
- **Why Abstractions.** Records travel over the existing `IDatagramFlow` seam, one flow per remote
  endpoint as ADR-0082 uses it for QUIC, and the retransmission timers and handshake limit run on
  `ExchangeContext.TimeProvider`. It constructs no socket.
- **What it holds.** No type moves: nothing exists yet. BL-412 adds the record layer (epochs,
  48-bit sequence numbers, the replay window, AES-GCM and AES-CCM_8 through the BCL's `AesGcm` and
  `AesCcm`), BL-413 the handshake messages with fragmentation and reassembly, BL-414 the
  certificate handshake with the TLS 1.2 PRF (RFC 5246 section 5, `HMACSHA256`), BL-415 the PSK
  mode and the alerts. Its public surface is a DTLS server session over an `IDatagramFlow` that
  yields an `IDatagramFlow` of decrypted application datagrams, so the CoAP server serves `coap`
  and `coaps` through one code path.
- **References nothing hand-built today.** The suites RFC 7252 mandates and the port's client
  offers (BL-403's context) are AES-GCM and AES-CCM_8 with P-256, RSA and ECDSA, all in the BCL.
  If BL-403 chooses a suite that needs a hand-built primitive (ChaCha20-Poly1305), that ADR amends
  this row to reference `Surl.Cryptography.ChaCha20` and `Surl.Cryptography.Poly1305`.
- **Who references it:** `Surl.Extended.Protocol.Coap`.
- **Built by BL-412** (projects in `Cryptography/`, `Surl.slnx` under `/Cryptography/`, the row in
  `ProtocolIsolationTests.HorizontalLibraries`), then BL-413, BL-414 and BL-415. BL-403 decides the
  handshake.

### 4. Logins: through the existing contracts, no new library

An extended server reaches every login through the contracts in `Surl.Protocol.Abstractions`, as
the original servers do; none references `Surl.Authentication`, and `Surl.Console` passes the
implementations in.

| Server | Login | Contract |
| --- | --- | --- |
| Xmpp | SASL, with `SCRAM-SHA-1` and `SCRAM-SHA-256` (RFC 6120 section 6) | `ISaslAuthenticationPolicy` |
| Kafka | `SaslHandshake` and `SaslAuthenticate` (PLAIN, SCRAM) | `ISaslAuthenticationPolicy` |
| Amqp0, Amqp1 | AMQP 0-9-1's `Connection.Start`/`Start-Ok` mechanisms, AMQP 1.0's SASL layer | `ISaslAuthenticationPolicy` |
| Stomp, Nats, Redis, Mqtt5 | a user name and password (or token) in one command | `IAuthenticationPolicy` |
| S3, AzureStorage, GoogleCloudStorage, OciRegistry, McpServer, SignalR, SocketIo, DnsQuery | HTTP `Authorization` | `IHttpAuthenticationSession` |

- **SCRAM goes in `Surl.Authentication`** (BL-416), as one more mechanism behind
  `ISaslAuthenticationPolicy`: the server owns the framing, the policy every mechanism
  (ADR-0049 section 6). The Xmpp and Kafka servers then need nothing of their own for it.
- A login step one of these protocols has that the contracts cannot express - none is known
  today - is added to `Surl.Protocol.Abstractions` by that server's own ADR, never by referencing
  `Surl.Authentication`.

### 5. Object stores: `Surl.Content`, no shared object-store library

S3, AzureStorage, GoogleCloudStorage and OciRegistry each map their namespace onto `Surl.Content`'s
content store, already a horizontal library: a bucket, container or repository is a directory under
the served path, an object or blob a file in it, through `ContentStore`'s exposure checks
(ADR-0006 section 2) and `IContentFileSystem` (so tests run on `InMemoryContentFileSystem`).

- **No shared object-store library,** because the four share nothing above the file system: S3
  answers XML `ListBucketResult`, Azure XML `EnumerationResults`, Google Cloud Storage JSON, and an
  OCI registry content-addressed manifests and blobs by digest, each with its own naming rules,
  listing paging and error bodies. What they do share - files, directories, uploads, byte ranges -
  is `Surl.Content` already.
- Each server's own ADR decides its layout under the served path (OCI's blobs by digest, for
  example) and where any metadata a file system cannot hold lives.
- If two of them come to need the same code above `Surl.Content`, an ADR amends this decision and
  adds a library; a server never copies the other's code into itself to avoid it.

## Alternatives considered

- **Moving HTTP/1.1 serving into `Surl.HttpMessage`.** Rejected: the RTSP server would then carry
  chunked bodies, `100 Continue` and HTTP keep-alive it must never use, and `Surl.HttpMessage`
  would need `Surl.Content` for the answer body (decision 1).
- **An `HttpAnswerBody` that knows no content store,** so `Surl.HttpServing` could skip
  `Surl.Content`. Rejected: it reshapes ADR-0081 decision 8's answer before BL-366 has built it,
  for a reference that costs nothing - `Surl.Content` is horizontal and references only
  Abstractions.
- **Extended servers writing their own HTTP/1.1 loop.** Rejected: nine servers would each
  re-implement chunked bodies, draining and the lingering close, each a place for a defect that
  the HTTP server has already been measured against.
- **One `Surl.HttpServing` holding WebSocket too.** Rejected: the WebSocket server needs framing
  but not HTTP/1.1 answering, and SignalR's negotiate request needs answering but not framing.
- **DTLS in `Foundation/`.** Rejected: decision 3.

## Consequences

Good:

- Every extended server listed in ADR-0083 has a horizontal home for what it shares with an
  original server, so none needs a forbidden reference, and the isolation test stays the rule.
- The HTTP, WebSocket and RTSP servers keep their bytes; each move is a refactor under the tests
  already pinning them.

Costs and caveats:

- BL-407 cannot start before BL-366, so the HTTP-based extended servers wait on both.
- `IHttpConnectionTakeover` and `WebSocketUpgradeAcceptance` are new names; BL-407 and BL-408 add
  them to `Documentation/Wiki/Glossary.md` as they build them.
- HTTP/2 and HTTP/3 sharing is still open; BL-402 decides it and may move more of
  `Surl.Protocol.Http` into a library of its own.
