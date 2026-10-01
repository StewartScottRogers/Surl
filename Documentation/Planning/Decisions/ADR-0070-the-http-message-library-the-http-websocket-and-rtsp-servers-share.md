# ADR-0070 — The HTTP message library the HTTP, WebSocket and RTSP servers share: `Surl.HttpMessage`

- **Status:** Accepted
- **Date:** 2026-09-30
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-09-30,
  in BL-281.
- **Amends:** [ADR-0002](ADR-0002-mirror-the-curl-ports-project-map.md) decision 3: its table of
  horizontal libraries gains the row of decision 1 below. Everything else in ADR-0002 stands.

## Context

Three protocol servers speak HTTP/1.x's message syntax (RFC 9112): `Surl.Protocol.Http`, which
has it today; `Surl.Protocol.Ws`, whose opening handshake is an HTTP/1.1 `GET` with `Upgrade`,
`Connection`, `Sec-WebSocket-Key`, `Sec-WebSocket-Version`, `Host` and perhaps `Authorization`,
answered `101 Switching Protocols` or refused with an HTTP response (RFC 6455 section 4.2); and
`Surl.Protocol.Rtsp`, whose RTSP/1.0 requests and responses are HTTP/1.1's syntax with
`RTSP/1.0` as the version (RFC 2326 sections 4, 6 and 7), `CSeq` echoed, a `Content-Length`
body, and HTTP's `WWW-Authenticate` challenges (RFC 2326 section 11).

Protocol servers may not reference each other (ADR-0002 decision 3, root `CLAUDE.md`), so what
they share lives in a horizontal library that joins ADR-0002's table through an ADR, as
ADR-0050 did for the mail servers' `Surl.MailStore` and `Surl.LineProtocol`.

Everything the other two servers need is today internal to, or public in,
`Surl.Protocol.Http.UnitLibrary`: `HttpConnectionReader` (the request head read bounded by
`ExchangeLimits.MaxRequestHeadBytes`, ADR-0006 and ADR-0019), `HttpRequestHeadLineReader`,
`HttpRequestHeadReadOutcome`, `HttpRequestHeadReadResult`, `HttpRequestLineParser` (which accepts
only `HTTP/1.x`), `HttpFieldLineParser`, `HttpRequestField`, `HttpRequestHead`, `HttpResponseHead`
(which always writes `HTTP/1.1`), `HttpSyntax`, `HttpStatus`, `HttpRequestBodyFraming`, the head
timeout in `HttpProtocolServer.ReadNextHeadAsync`, and the `WWW-Authenticate` writing in
`HttpRequestResponder` (one field per value `IHttpAuthenticationSession.JudgeAsync` gives,
ADR-0032 section 6).

This ADR pins no byte upstream curl sends or expects: RFC 9112, RFC 6455 and RFC 2326 give the
syntax, the HTTP server's bytes do not change (decision 4), and the WebSocket and RTSP decision
tasks (BL-285, BL-286) measure what pinned upstream curl sends and accepts, as ADR-0003 requires.

## Decision

### 1. One library, and its row in ADR-0002's table

| Horizontal library | May itself reference | Holds |
| --- | --- | --- |
| `Surl.HttpMessage.UnitLibrary` | `Surl.Protocol.Abstractions.UnitLibrary` | decision 2's HTTP/1.x message machinery |

- The planning assumption BL-281 was filed on is adopted. Namespace `Surl.HttpMessage`.
- A production library held to the solution's quality gates, AOT-compatible, touching no
  socket: it reads and writes only through `IConnection`, and times only with
  `ExchangeContext.TimeProvider`.
- `Surl.Protocol.Http` (BL-293), `Surl.Protocol.Ws` (BL-301) and `Surl.Protocol.Rtsp` (BL-313)
  reference it. Any other protocol server may, as ADR-0002 allows for every row.
- It may not reference `Surl.Content`, `Surl.LineProtocol`, `Surl.Networking` or any protocol
  server. **Why not `Surl.LineProtocol`:** its lines end only at CRLF (ADR-0050 decision 8),
  while an HTTP/1.x head line ends at LF with or without a CR before it (RFC 9112 section 2.2),
  and its bound is per line, while an HTTP head is bounded as a whole by `--max-request-head`.
- BL-292 creates the two projects and the isolation test's row; BL-293 moves the code.

### 2. What moves, and its public surface

Every type keeps its name: the syntax is HTTP/1.x's, and RTSP borrows it by name (RFC 2326
section 1.1 calls RTSP "intentionally similar in syntax and operation to HTTP/1.1"). One
concept keeps one name, so the glossary's "request head", "request line" and "field line" stay
the same terms for all three servers. The types a server uses are `public`, as
`Surl.LineProtocol`'s are; helpers stay `internal` with `InternalsVisibleTo` the library's own
tests.

| Type | Visibility | Change on moving |
| --- | --- | --- |
| `HttpMessageProtocol` (new) | public | The protocol name and highest minor version a reader accepts and a response head writes: `HttpMessageProtocol.Http11` (`HTTP`, 1) and `HttpMessageProtocol.Rtsp10` (`RTSP`, 0). Major version 1 only. |
| `HttpConnectionReader` | public | Its constructor takes the `HttpMessageProtocol` to accept. It gains `ReadNextRequestHeadAsync(ExchangeContext context, bool isFirstHead)`, `HttpProtocolServer.ReadNextHeadAsync`'s head-timeout logic moved (decision 3). `ReadLineAsync`, `ReadAsync`, `WaitForBytesAsync` and `HasReceivedHeadBytes` move unchanged. |
| `HttpRequestHeadLineReader` | internal | Carries the protocol to the request-line parser. |
| `HttpRequestLineParser` | internal | Accepts `<name>/1.<digit>` for the protocol it is given only; any other name is `MalformedRequestLine`, another major version `UnsupportedVersion`, and a minor version above the protocol's highest is read as the highest (RFC 9110 section 2.5; RFC 2326 section 3.1 versions RTSP as HTTP does). `HTTP/1.0` and `HTTP/1.1` read as today. |
| `HttpFieldLineParser`, `HttpSyntax` | internal | None. |
| `HttpRequestField`, `HttpRequestHeadReadResult`, `HttpRequestHeadReadOutcome` | public | None. |
| `HttpRequestHead` | public | Gains `Protocol`, the `HttpMessageProtocol` its request line named. |
| `HttpStatus` | public | Keeps its well-known HTTP statuses; a server builds any other with the record's constructor (RTSP's `454 Session Not Found`, for example, in `Surl.Protocol.Rtsp`). |
| `HttpResponseHead` | public | Its constructor takes the `HttpMessageProtocol` whose status line it writes (`HTTP/1.1 …`, `RTSP/1.0 …`). `ServerName` (`surl`, ADR-0006 section 3) moves with it. Gains `AddChallengeFields(IReadOnlyList<string> values)`: one `WWW-Authenticate` field per value, in order (ADR-0032 section 6), the loop now in `HttpRequestResponder`. |
| `HttpRequestBodyFraming`, `HttpRequestBodyFramingKind` | public | None: a head's `Content-Length` and `Transfer-Encoding` judged by RFC 9112 section 6.3. RTSP answers a chunked framing as its own ADR decides (RFC 2326 has no chunked bodies). |

### 3. The head timeout moves with the reader

Each of the three servers must time a head the same way (ADR-0006 section 1): the first head
from the reader's creation, a later one from its first byte, `HeadTimedOut` or
`HeadTimedOutBeforeAnyByte` by whether a byte arrived, a timeout past `uint.MaxValue - 1`
milliseconds treated as none, and cancellation of the exchange itself left to propagate. Three
copies would drift, so `HttpConnectionReader.ReadNextRequestHeadAsync` holds it once.

### 4. What stays in `Surl.Protocol.Http`

`HttpProtocolServer`, `HttpRequestResponder` (method dispatch, content responses, the
authentication flow, `100 Continue` per ADR-0027), `HttpConnectionPersistence`,
`HttpRequestBodyDiscarder` and `HttpRequestBodyDiscardOutcome` (chunked and counted bodies read
and discarded), `HttpUnreadRequestDrainer` and `ConnectionWriteStream`. Which status answers
which `HttpRequestHeadReadOutcome` (`400`, `431`, `408`, `505`) stays each server's. The move
changes no byte the HTTP server sends: every existing `Surl.Protocol.Http.UnitTests` test passes
unchanged (BL-293).

### 5. The glossary

The "request head", "request line" and "field line" rows keep their type names; BL-293 adds
"in `Surl.HttpMessage`" to their code column when the types move, and the "request head" row
names the three servers that read one.

### 6. The WebSocket upgrade is answered only on `ws://` and `wss://` listen URLs

An `http://` listener's `HttpProtocolServer` treats a request carrying `Upgrade: websocket` as
the ordinary request it also is and answers it as it does today, ignoring `Upgrade` as RFC 9110
section 7.8 lets a server do; upstream curl then reports the refused upgrade as its own
behaviour says (BL-285 measures it). **Why:** a listen URL names the protocol a surl listener
answers (the command-line rule, `surl [options] <url>`), `ws://` and `wss://` are the schemes
upstream curl's WebSocket requests use, and answering the upgrade on `http://` too would need
the HTTP server to hand a connection to the WebSocket server - a new contract in
`Surl.Protocol.Abstractions` for something no curl request needs. No task is filed for it.

## Alternatives considered

- **Each server keeps its own reader.** Rejected: three bounded head readers, three head
  timeouts and three field parsers to keep in step, each a place for a request-smuggling or
  limit defect (ADR-0006) to be fixed in one copy and not the others.
- **Put the machinery in `Surl.Protocol.Abstractions`.** Rejected: Abstractions holds contracts,
  and changing it serialises every protocol task on the board.
- **Build on `Surl.LineProtocol`.** Rejected in decision 1: CRLF-only lines and a per-line bound
  are not HTTP's rules.
- **Neutral names (`MessageHead`, `RequestHead`).** Rejected in decision 2: the syntax is
  HTTP's by definition in both RFCs, and a rename would give one concept two names in the
  glossary's history for no gain.
- **Answer the WebSocket upgrade on `http://` listeners.** Rejected in decision 6.

## Consequences

- ADR-0002 decision 3's table gains one row; `ProtocolIsolationTests` learns it in BL-292.
- The product overview's "Layers" and "Project layout" name the library as intent until BL-292
  creates it.
- BL-293 moves the types of decision 2 and adds tests for what generalising added: `RTSP/1.0`
  accepted when the reader is given `HttpMessageProtocol.Rtsp10` and refused when given
  `Http11`, and a response head written as `RTSP/1.0`.
- BL-301 and BL-313 build the WebSocket and RTSP servers on the library without referencing
  `Surl.Protocol.Http`.
