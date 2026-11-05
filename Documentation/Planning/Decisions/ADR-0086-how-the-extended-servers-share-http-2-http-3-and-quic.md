# ADR-0086 — How the extended servers share HTTP/2, HTTP/3 and QUIC

- **Status:** Accepted
- **Date:** 2026-11-04
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04,
  in BL-402. BL-409 builds `Surl.Http2` (decision 1), BL-410 builds `Surl.Http3` (decision 2),
  BL-411 adds extended CONNECT and HTTP datagrams to it (decision 3), BL-572 lets a server other
  than HTTP/3 ride `Surl.Quic` (decision 4), and BL-573 adds QUIC DATAGRAM frames to `Surl.Quic`
  (decision 4).
- **Amends:** [ADR-0002](ADR-0002-mirror-the-curl-ports-project-map.md) decision 3 (two new rows,
  and the `Surl.Quic` row gains `Surl.Protocol.Abstractions.UnitLibrary`; decision 5),
  [ADR-0081](ADR-0081-how-surl-serves-http-2.md) decision 4 (frames, streams, flow control and the
  connection move from `Surl.Protocol.Http` to `Surl.Http2`; decision 1),
  [ADR-0082](ADR-0082-how-surl-serves-http-3-over-quic.md) decision 2 (the HTTP/3 frames, control
  and QPACK streams move to `Surl.Http3`; decision 2) and its decision 3's ALPN rule (the TLS 1.3
  handshake agrees the ALPN IDs its caller names; decision 4), and
  [ADR-0084](ADR-0084-the-libraries-the-extended-servers-share-for-http-1-1-websocket-and-dtls.md)'s
  rule that every HTTP/2 and HTTP/3 type stays in `Surl.Protocol.Http`. Every byte those ADRs pin
  for `surl http://` and `surl https://` stands unchanged.

## Context

[ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
lets an extended server (`Surl.Extended.Protocol.*`) reference only `Surl.Protocol.Abstractions`
and the horizontal libraries of ADR-0002's table, never an original protocol server. Three
extended servers need HTTP/2, HTTP/3 or QUIC:

- **gRPC** (`grpc`, `grpcs`; grpc/grpc `doc/PROTOCOL-HTTP2.md`) speaks HTTP/2: prior knowledge
  for `grpc`, ALPN `h2` for `grpcs`. It needs full-duplex streams (a response may start before the
  request body ends), trailers (a `HEADERS` frame with `END_STREAM` after the `DATA` frames,
  RFC 9113 section 8.1), `RST_STREAM` and `PING`.
- **WebTransport** (`webtransport`; draft-ietf-webtrans-http3-16) speaks HTTP/3 and needs, beyond
  what the HTTP server uses: `SETTINGS_ENABLE_CONNECT_PROTOCOL` (RFC 9220), an extended CONNECT
  with `:protocol` `webtransport`, HTTP datagrams (RFC 9297, `SETTINGS_H3_DATAGRAM`) over QUIC
  DATAGRAM frames (RFC 9221, the `max_datagram_frame_size` transport parameter), capsules, its own
  `SETTINGS`, and the WebTransport stream types: a bidirectional stream opening with the signal
  value `0x41` and a unidirectional stream of type `0x54`, each followed by the session ID, which
  the HTTP/3 connection must hand to the session they name.
- **DNS over QUIC** (`dns`, RFC 9250) speaks QUIC on ALPN `doq` with no HTTP/3 at all. DNS over
  HTTPS may also ride HTTP/2 (BL-478 decides).

ADR-0081 decision 4 puts HPACK in `Surl.Hpack` and HTTP/2's frames, streams, flow control and
connection in `Surl.Protocol.Http`. ADR-0082 decision 2 puts QUIC in `Surl.Quic`, TLS 1.3 in
`Surl.Tls13` and QPACK in `Surl.Qpack`, each its own horizontal library, and the HTTP/3 frames,
control and QPACK streams and server in `Surl.Protocol.Http`. So QUIC, TLS 1.3, HPACK and QPACK are
already shareable; only the HTTP/2 and HTTP/3 connections are not. ADR-0082 also fixes two things
a second QUIC user cannot live with: the handshake agrees ALPN `h3` and nothing else (decision 3),
and the datagram extension is not offered (decision 4). And it names no place for the loop that
drives a `Surl.Quic` connection from an `IDatagramFlow` (decision 5); BL-383 would build it inside
`Surl.Protocol.Http`, where DNS over QUIC could not reach it.

None of the HTTP/2 or HTTP/3 code exists yet: BL-367 to BL-370 and BL-383 build it. The type names
below are planning names; where a building task names a type differently, the type that does the
job named here moves.

This ADR pins no byte any client sends or expects. The original servers' bytes do not change (each
extraction task's acceptance says so), and each extended server's own ADR cites the specification
sections its bytes come from, as ADR-0083 decision 1 requires.

## Decision

### 1. HTTP/2: a new `Surl.Http2.UnitLibrary`

| Horizontal library | Folder | May itself reference | Holds |
| --- | --- | --- | --- |
| `Surl.Http2.UnitLibrary` | `Protocols/Http/` | `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.Hpack.UnitLibrary` | an HTTP/2 connection on one `IConnection` (RFC 9113): frames, streams, flow control, `SETTINGS`, `PING`, `RST_STREAM`, `GOAWAY`, the ADR-0006 limits of ADR-0081 decision 5, and request streams handed to a handler |

- **What moves, type by type**, from `Surl.Protocol.Http` (namespace `Surl.Http2`; `public` only
  what a server uses, the rest `internal` with `InternalsVisibleTo` the library's own tests):

  | Type (planning name) | Built by | Visibility |
  | --- | --- | --- |
  | `Http2Frame`, `Http2FrameType`, `Http2FrameFlags`, `Http2FrameReader`, `Http2FrameWriter` (the frame codec) | BL-367 | internal |
  | `Http2ErrorCode` (RFC 9113 section 7) | BL-367 | public |
  | `Http2SettingsParameter`, `Http2Settings` (both peers' settings, and `HTTP2-Settings` decoding for `Upgrade: h2c`) | BL-367, BL-369 | public |
  | `Http2ConnectionPreface` (the 24 preface bytes, so a listener can tell HTTP/2 from an HTTP/1.x head) | BL-368 | public |
  | `Http2Stream`, `Http2StreamState` (RFC 9113 section 5.1) | BL-368 | internal |
  | `Http2FlowControlWindow` (stream and connection windows, `WINDOW_UPDATE`) | BL-368 | internal |
  | the request-header validation of RFC 9113 section 8.2 and 8.3 (pseudo-headers, uppercase names, connection-specific fields; `RST_STREAM` `PROTOCOL_ERROR`) | BL-368 | internal |
  | the limit mapping of ADR-0081 decision 5 (`GOAWAY` `ENHANCE_YOUR_CALM`, `FRAME_SIZE_ERROR`, `REFUSED_STREAM`, the idle and shutdown `GOAWAY`) | BL-368 | internal |
  | `Http2ConnectionServer` (the connection loop: preface, `SETTINGS` and its ACK, `PING`, stream concurrency, `GOAWAY`) | BL-368 | public |
  | `IHttp2StreamHandler`, `Http2RequestStream`, `Http2RequestHead` (new; below) | BL-409 | public |

- **What stays in `Surl.Protocol.Http`:** `HttpRequestAnswerer`; `Http2StreamAnswerWriter`
  (ADR-0081 decision 8), which becomes an `IHttp2StreamHandler` that builds an `HttpRequest`
  from the `Http2RequestStream` and writes the `HttpAnswer` back through it; telling the preface
  from an HTTP/1.x head; the `Upgrade: h2c` checks on the HTTP/1.1 side (ADR-0081 decision 3); the
  `HTTP_1_1_REQUIRED` answer to a connection-bound login; and the log lines.
- **How a server receives a stream.**

  ```csharp
  public interface IHttp2StreamHandler
  {
      Task ServeStreamAsync(Http2RequestStream stream, CancellationToken cancellationToken);
  }
  ```

  A server constructs `Http2ConnectionServer` with its handler, its `Http2Settings` and the
  `ExchangeContext`, and awaits `ServeAsync(IConnection)` (the client's preface is the first
  thing read) or `ServeAfterUpgradeAsync(IConnection, Http2Settings clientSettings,
  Http2RequestHead firstRequest)` (stream 1 already half-closed, ADR-0081 decision 3). The
  connection calls the handler once per request stream that passed validation, concurrently up to
  `MAX_CONCURRENT_STREAMS`. `Http2RequestStream` gives the handler:
  - `Head` - the `Http2RequestHead`: `:method`, `:scheme`, `:authority`, `:path` and the regular
    fields, decoded by HPACK and already validated;
  - `ReadBodyAsync` - the request's `DATA`, returning flow-control credit as the handler consumes
    it, and `RequestTrailers` once the body has ended;
  - `WriteHeadersAsync(fields, endStream)`, `WriteDataAsync(bytes, endStream)` and
    `WriteTrailersAsync(fields)` - the response, split into `CONTINUATION` and `DATA` frames no
    larger than the peer allows and sent only as its window allows; `WriteTrailersAsync` sends a
    `HEADERS` frame with `END_STREAM` after the `DATA` frames (RFC 9113 section 8.1);
  - `ResetAsync(Http2ErrorCode)` - `RST_STREAM`.

  Reading and writing are independent, so a response may start before the request body ends:
  gRPC's bidirectional streaming. The HTTP server reads first and writes after, so its bytes do
  not change. `PING` is answered by the connection; no handler sees it.
- **No extended CONNECT over HTTP/2.** ADR-0081 decision 9 stands: no extended server needs it
  (gRPC sends ordinary `POST`s), so `SETTINGS_ENABLE_CONNECT_PROTOCOL` is never sent and a
  `:protocol` pseudo-header is `RST_STREAM` `PROTOCOL_ERROR` in the library itself.
- **Who references it:** `Surl.Protocol.Http`, `Surl.Extended.Protocol.Grpc`, and
  `Surl.Extended.Protocol.DnsQuery` if BL-478 serves DNS over HTTPS on HTTP/2.
- **Built by BL-409**, after BL-369 and BL-370, which built the HTTP/2 connection inside
  `Surl.Protocol.Http`. BL-409 creates the projects beside `Surl.Hpack` in `Protocols/Http/`, lists
  them in `Surl.slnx` under `/Protocols/Http/`, moves the types and their tests, adds trailers if
  the HTTP server never needed them, and adds the row to `ProtocolIsolationTests`.

### 2. HTTP/3: a new `Surl.Http3.UnitLibrary`

| Horizontal library | Folder | May itself reference | Holds |
| --- | --- | --- | --- |
| `Surl.Http3.UnitLibrary` | `Protocols/Http/` | `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.Quic.UnitLibrary`, `Surl.Qpack.UnitLibrary` | an HTTP/3 connection on one `Surl.Quic` connection (RFC 9114): frames, the control and QPACK streams, `SETTINGS`, `GOAWAY`, the ADR-0006 limits of ADR-0082 decision 7, request streams handed to a handler, and (decision 3) extended CONNECT, HTTP datagrams, capsules and claimed stream types |

- **What moves, type by type**, from `Surl.Protocol.Http` (namespace `Surl.Http3`; the same
  visibility rule):

  | Type (planning name) | Built by | Visibility |
  | --- | --- | --- |
  | `Http3Frame`, `Http3FrameType`, `Http3FrameReader`, `Http3FrameWriter` (`DATA`, `HEADERS`, `SETTINGS`, `GOAWAY`, `MAX_PUSH_ID`, `CANCEL_PUSH`, `PRIORITY_UPDATE` read and ignored, reserved types ignored) | BL-383 | internal |
  | `Http3ErrorCode` (RFC 9114 section 8.1) | BL-383 | public |
  | `Http3SettingsParameter`, `Http3Settings` | BL-383 | public |
  | `Http3UnidirectionalStreamType` and the control, QPACK encoder and QPACK decoder streams (opened by the server, accepted from the client, a closed one `H3_CLOSED_CRITICAL_STREAM`) | BL-383 | internal |
  | the request-stream reader (`HEADERS`, `DATA`, trailing `HEADERS`) and the malformed-message checks (`H3_MESSAGE_ERROR`) | BL-383 | internal |
  | the limit mapping of ADR-0082 decision 7 (`H3_REQUEST_INCOMPLETE`, the `:status 431` for an over-long field section, `H3_EXCESSIVE_LOAD`, the shutdown `GOAWAY`) | BL-383 | internal |
  | `Http3ConnectionServer` (an `IQuicApplicationProtocol`, decision 4) | BL-383 | public |
  | `IHttp3RequestStreamHandler`, `Http3RequestStream`, `Http3RequestHead` (new; below) | BL-410 | public |

- **What stays in `Surl.Protocol.Http`:** `HttpRequestAnswerer`; BL-383's HTTP/3 answer writer,
  which becomes an `IHttp3RequestStreamHandler` as `Http2StreamAnswerWriter` becomes an HTTP/2
  one; the `IDatagramProtocolServer` for an `https` listener's UDP side, which composes
  `QuicDatagramFlowServer` (decision 4) with `Http3ConnectionServer`; the `H3_VERSION_FALLBACK`
  answer to a connection-bound login; `Alt-Svc`; and the log lines.
- **How a server receives a request stream.**

  ```csharp
  public interface IHttp3RequestStreamHandler
  {
      Task ServeRequestStreamAsync(Http3RequestStream stream, CancellationToken cancellationToken);
  }
  ```

  `Http3RequestStream` has the same shape as `Http2RequestStream`: `Head` (decoded by QPACK and
  validated), `ReadBodyAsync`, `WriteHeadersAsync`, `WriteDataAsync`, and `ResetAsync(Http3ErrorCode)`,
  which sends `RESET_STREAM` and `STOP_SENDING`; the response ends with the QUIC stream's FIN.
  `Http3ConnectionServer` is constructed with the request handler, an `Http3ServerOptions` (the
  `SETTINGS` it sends, and decision 3's registrations) and the `ExchangeContext`. The HTTP server's
  options carry exactly ADR-0082's `SETTINGS`, so its bytes do not change.
- **Who references it:** `Surl.Protocol.Http` and `Surl.Extended.Protocol.WebTransport`.
- **Built by BL-410**, after BL-384 serves HTTP/3 from the executable. BL-410 creates the
  projects beside `Surl.Qpack` in `Protocols/Http/`, lists them in `Surl.slnx`, moves the types and
  their tests, and adds the row to `ProtocolIsolationTests`. It only moves code.

### 3. Extended CONNECT, HTTP datagrams and claimed stream types, in `Surl.Http3`

These are added to `Surl.Http3` and used by WebTransport only. The HTTP server registers nothing,
so its `SETTINGS` and every other byte stay as ADR-0082 decision 9 says: no extended CONNECT, no
HTTP datagrams.

- **Registration.** A server adds an `IHttp3ExtendedConnectHandler` to its `Http3ServerOptions`:

  ```csharp
  public interface IHttp3ExtendedConnectHandler
  {
      string Protocol { get; }                       // the :protocol value, e.g. "webtransport"
      long? BidirectionalStreamSignal { get; }       // e.g. 0x41, or null
      long? UnidirectionalStreamType { get; }        // e.g. 0x54, or null
      Task ServeSessionAsync(Http3ExtendedConnectSession session, CancellationToken cancellationToken);
  }
  ```

  With at least one registered, the server's `SETTINGS` carry `SETTINGS_ENABLE_CONNECT_PROTOCOL`
  = 1 (RFC 9220 section 3) and `SETTINGS_H3_DATAGRAM` = 1 (RFC 9297 section 2.1.1), and
  `Http3ServerOptions` may carry further `SETTINGS` parameters by identifier and value - the
  WebTransport settings, whose identifiers and values BL-457's ADR takes from the pinned draft.
  The library implements RFC 9220 and RFC 9297 and knows nothing of WebTransport itself.
- **Extended CONNECT.** A `CONNECT` request with `:protocol` naming a registered handler reaches
  that handler's `ServeSessionAsync` instead of the request-stream handler; one naming no
  registered handler is answered as RFC 9220 says, which BL-411 pins from the RFC's text.
  `Http3ExtendedConnectSession` gives the handler the request head, `WriteHeadersAsync` for its
  answer (a 2xx accepts the session), capsules read and written on the request stream (RFC 9297
  section 3: type and length, unknown types skipped), HTTP datagrams received and sent with the
  quarter stream ID handled by the library (RFC 9297 section 2.1), and the session's claimed
  streams.
- **Claimed stream types.** A client bidirectional stream that opens with a registered
  `BidirectionalStreamSignal`, or a unidirectional stream of a registered
  `UnidirectionalStreamType`, is read as signal or type, then a session ID, and handed to the
  `Http3ExtendedConnectSession` that ID names, as a raw `Surl.Quic` stream; the session can open
  such streams too, with the prefix written for it. What happens to a stream naming no open session
  follows the pinned draft (BL-457's ADR, built by BL-411). A stream type nobody registered is
  handled as RFC 9114 section 6.2 and section 9 say today, so the HTTP server is unchanged.
- **Built by BL-411**, after BL-410 and BL-573.

### 4. Riding `Surl.Quic` without HTTP/3, and what `Surl.Quic` gains

DNS over QUIC rides `Surl.Quic` directly, so `Surl.Quic` - not `Surl.Http3` and not
`Surl.Protocol.Http` - holds everything a QUIC server needs below its application protocol:

- **The flow loop.** `QuicDatagramFlowServer` (new, public) takes one `IDatagramFlow` (ADR-0004,
  ADR-0082 decision 5), feeds its datagrams to a `Surl.Quic` server connection, sends what the
  connection returns through `SendAsync`, waits for the connection's next deadline on the injected
  `TimeProvider`, and, once the handshake completes, hands the connection's streams to an
  application protocol:

  ```csharp
  public interface IQuicApplicationProtocol
  {
      Task ServeAsync(QuicServerConnection connection, CancellationToken cancellationToken);
  }
  ```

  `QuicServerConnection` is the stream API of BL-378: accept client streams, open server streams,
  read and write with flow control, reset, close the connection with an application error code.
  `Http3ConnectionServer` is one `IQuicApplicationProtocol`; the DNS server's DoQ handler is
  another. The connection core still does no I/O (ADR-0082 decision 2); only this loop touches the
  flow, so `Surl.Quic`'s ADR-0002 row gains `Surl.Protocol.Abstractions.UnitLibrary`.
- **The caller's ALPN and transport parameters.** `QuicServerConnectionOptions` names the ALPN IDs
  the TLS 1.3 handshake agrees to (`Surl.Tls13` takes the list from its caller; a ClientHello
  offering none of them ends with `no_application_protocol`, RFC 9001 section 8.1) and the
  transport parameters the server sends. The HTTP server passes `h3` and exactly ADR-0082
  decision 4's parameters, so its bytes do not change; DNS over QUIC passes `doq` and the
  parameters BL-478's ADR decides (RFC 9250 uses client bidirectional streams only).
- **QUIC DATAGRAM frames (RFC 9221).** Off unless the caller's options set
  `max_datagram_frame_size`; then the parameter is sent, `DATAGRAM` frames (types `0x30` and
  `0x31`) are received and sent through `QuicServerConnection`, are ack-eliciting and counted by
  congestion control but never retransmitted (RFC 9221 section 5), and a `DATAGRAM` frame received
  without the parameter having been sent ends the connection with `PROTOCOL_VIOLATION` (section 3).
  The HTTP server never sets it (ADR-0082 decision 4 stands for it); WebTransport does.
- **Who references `Surl.Quic`:** `Surl.Http3`, `Surl.Protocol.Http` (to compose the flow loop),
  `Surl.Extended.Protocol.WebTransport` and `Surl.Extended.Protocol.DnsQuery`.
- **Built by BL-572** (the flow loop, `IQuicApplicationProtocol` and the caller's ALPN and
  transport parameters), which BL-383 and BL-484 depend on, and **BL-573** (DATAGRAM frames),
  which BL-411 depends on.

### 5. ADR-0002's table and the isolation test

ADR-0002 decision 3's table gains two rows, and one row changes:

| Horizontal library | May itself reference |
| --- | --- |
| `Surl.Http2.UnitLibrary` (ADR-0086) | `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.Hpack.UnitLibrary` |
| `Surl.Http3.UnitLibrary` (ADR-0086) | `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.Quic.UnitLibrary`, `Surl.Qpack.UnitLibrary` |
| `Surl.Quic.UnitLibrary` (ADR-0082, ADR-0086) | `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.Tls13.UnitLibrary`, `Surl.Cryptography.ChaCha20.UnitLibrary`, `Surl.Cryptography.Poly1305.UnitLibrary` |

`Surl.Protocol.Abstractions` references nothing, so no cycle forms. BL-409 and BL-410 add their
rows to `ProtocolIsolationTests.HorizontalLibraries`, and BL-572 widens `Surl.Quic`'s.

### 6. ALPN and ports: servers never share a listener

Each listen URL is its own listener, and ALPN never routes a connection from one
server to another. Within a listener, the ALPN IDs offered are that server's own list:

| Listen URL | Transport | ALPN offered | Connection served by |
| --- | --- | --- | --- |
| `https://` | TCP + TLS | `h2`, `http/1.1` (ADR-0081 decision 2) | `Surl.Protocol.Http`: `Surl.Http2` or `Surl.HttpServing` |
| `https://` with `--http3` | UDP + QUIC | `h3` (ADR-0082) | `Surl.Protocol.Http` through `Surl.Http3` |
| `grpc://` | TCP | none; prior knowledge | `Surl.Extended.Protocol.Grpc` through `Surl.Http2` |
| `grpcs://` | TCP + TLS | `h2` | `Surl.Extended.Protocol.Grpc` through `Surl.Http2` |
| `webtransport://` | UDP + QUIC | `h3` | `Surl.Extended.Protocol.WebTransport` through `Surl.Http3` |
| `dns://` asking for DoQ (BL-478) | UDP + QUIC | `doq` | `Surl.Extended.Protocol.DnsQuery` through `Surl.Quic` |

Two listen URLs that need the same address, port and transport cannot both bind; `surl` reports
that as it reports any listener that cannot bind, so an `https://` listener with `--http3` and a
`webtransport://` listener need different ports. Neither `Surl.Http2` nor `Surl.Http3` reads ALPN:
the server that composes them does, and what a `grpcs` server does with a TLS client that offered
no ALPN is the gRPC ADR's to decide.

## Alternatives considered

- **Build HTTP/2 and HTTP/3 straight into `Surl.Http2` and `Surl.Http3`,** re-filing BL-367 to
  BL-370 and BL-383. Rejected: those tasks are filed, sized and ordered against
  `Surl.Protocol.Http`'s tests, and BL-366 is in progress beside them. Moving the code afterwards
  costs one task each and gives the strongest proof the move changed nothing: every
  `Surl.Protocol.Http.UnitTests` expected byte passes unedited.
- **One `Surl.HttpFraming` library for both.** Rejected: HTTP/2 rides an `IConnection` and HPACK,
  HTTP/3 rides `Surl.Quic` and QPACK; they share no frame, codec or state, and one library would
  make gRPC reference QUIC and WebTransport reference HPACK.
- **Hand extended servers `HttpAnswer` through ADR-0084's `IHttpRequestHandler`.** Rejected for
  HTTP/2 and HTTP/3: an `HttpAnswer` is decided whole before it is written, so it cannot carry
  gRPC's bidirectional streaming or trailers, nor a WebTransport session. The stream handlers here
  are one level lower, and the HTTP server adapts `HttpAnswer` to them, so its own answering stays
  version-independent (ADR-0081 decision 8).
- **The QUIC flow loop in `Surl.Http3`.** Rejected: DNS over QUIC would then reference HTTP/3 for
  a loop that has nothing to do with HTTP.
- **Extended CONNECT over HTTP/2 too (RFC 8441), for symmetry.** Rejected: no extended server
  needs it, and ADR-0081 decision 9's `PROTOCOL_ERROR` answer is measured against nothing that
  would change.
- **Routing one port between HTTP and an extended server by ALPN.** Rejected: each listen URL is
  already its own listener, so a shared port would need a new listener contract for no client
  that asks for one.

## Consequences

Good:

- gRPC, WebTransport and DNS over QUIC reach every hand-built HTTP/2, HTTP/3 and QUIC piece
  without referencing `Surl.Protocol.Http`, and the isolation test proves it.
- The HTTP server's bytes cannot drift: each extraction task's acceptance is that every existing
  expected byte passes unedited.

Costs and caveats:

- Two more libraries, each held to the quality gates, and two more tasks (BL-572, BL-573) on
  `Surl.Quic`'s path.
- The type names above are planning names until BL-367 to BL-370 and BL-383 build the code;
  BL-409 and BL-410 move whatever does each job named here.
- The WebTransport settings, the treatment of a stream naming no open session, and the answer to
  an unregistered `:protocol` are left to the pinned draft and RFC 9220, through BL-457's ADR and
  BL-411.
