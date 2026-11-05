# ADR-0081 — How Surl serves HTTP/2

- **Status:** Accepted
- **Amended by:** [ADR-0086](ADR-0086-how-the-extended-servers-share-http-2-http-3-and-quic.md)
  (2026-11-04, BL-402): decision 4's frames, streams, flow control and connection move from
  `Surl.Protocol.Http` to the horizontal library `Surl.Http2.UnitLibrary` (BL-409), which
  `Surl.Protocol.Http` and the gRPC server both reference. Every byte this ADR pins is unchanged.
- **Date:** 2026-11-04
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-04,
  in BL-364. BL-365 to BL-371 build what it decides.
- **Amends:** [ADR-0017](ADR-0017-curl-se-8-22-0-windows-build-as-a-supplementary-build.md)
  decision 3 (HTTP/2 on Windows is measured with the static 8.21.0 build, not 8.22.0_2; decision 1
  below), [ADR-0010](ADR-0010-the-server-side-tls-contract.md) section 4 (`https` offers `h2`;
  decision 2), [ADR-0002](ADR-0002-mirror-the-curl-ports-project-map.md) decision 3's table
  (`Surl.Hpack.UnitLibrary`; decision 4) and
  [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) section 5 for HTTP/2 connections
  (decision 5). Every other section of those ADRs applies to HTTP/2 as written.

## Context

The product overview puts HTTP/2 in scope ("HTTP versions: 1.0, 1.1, 2 and 3 over QUIC, on the
server side", Phasing row 6), and `Surl.Protocol.Http`'s `CLAUDE.md` says HTTP/2 is hand-built.
The base class library has no public HTTP/2 server framing and no HPACK, so both are written by
hand (root `CLAUDE.md`, "Decisions"). Today `surl http://` and `surl https://` speak HTTP/1.x only,
and `https` offers exactly the ALPN ID `http/1.1` (ADR-0010 section 4).

Upstream curl 8.21.0's documentation (tag `curl-8_21_0`, `docs/cmdline-opts/http2.md` and
`http2-prior-knowledge.md`): over HTTPS curl negotiates HTTP/2 in the TLS handshake by default;
over HTTP, `--http2` asks for it with `Upgrade: h2c`; `--http2-prior-knowledge` speaks HTTP/2 from
the first byte, and on HTTPS since 8.10.0 offers only `h2`. Builds with the `HTTP2` feature: the
`linux-x64` and `osx-arm64` reference builds, and on Windows only the two supplementary builds,
static 8.21.0 and curl.se's 8.22.0_2; the Git for Windows reference build (Schannel) has none.

### Measured

Every row was recorded on 2026-11-04 on Windows with `Record-CurlExchange.ps1` (BL-362's `-TlsAlpn`
for TLS, `-Raw` for the HTTP/2 bytes), against these pinned builds:

- **S** - stunnel/static-curl 8.21.0, `curl.exe`, SHA-256
  `589C8E4D297B4831C82ADF0261FC1CA57CE59D663B91B4106D2EE7DFF3972648` (OpenSSL 4.0.1, nghttp2 1.69.0).
- **W** - curl.se 8.22.0_2, `curl.exe`, SHA-256
  `B028548A8C0D3DC2C899FE415DCE1EF1D2406801B3ADC43DF9921683026E86CC` (LibreSSL 4.3.2, nghttp2 1.70.0).

The server's replies use these frames: **E** = empty `SETTINGS` (`00 00 00 04 00 00 00 00 00`);
**A** = `SETTINGS` ACK (`00 00 00 04 01 00 00 00 00`); **R** = `HEADERS` stream 1, `END_STREAM` and
`END_HEADERS`, block `88` (`:status 200`), then `DATA` stream 1, `END_STREAM`, `hi`.

What curl sends first, identical for every row that reaches HTTP/2 (165 bytes for a `GET`):

| Bytes | Frame | Decoded |
| --- | --- | --- |
| `50 52 49 20 2A ... 0D 0A 0D 0A` (24) | connection preface | `PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n` |
| `00 00 12 04 00 00 00 00 00` + 18 | `SETTINGS` | `MAX_CONCURRENT_STREAMS` (3) = 100, `INITIAL_WINDOW_SIZE` (4) = 65536, `ENABLE_PUSH` (2) = 0 |
| `00 00 04 08 00 00 00 00 00 3E 7F 00 01` | `WINDOW_UPDATE` stream 0 | increment 1048510465: a connection window of 1048576000 |
| `00 00 1F 01 05 00 00 00 01` + 31 | `HEADERS` stream 1, `END_STREAM`, `END_HEADERS` | `82` `:method GET`; `87` `:scheme https` (`86` for `http`); `41 8B` + Huffman `:authority 127.0.0.1:<port>`; `84` `:path /`; `7A 88` + Huffman `user-agent curl/8.21.0`; `53 03 2A 2F 2A` `accept */*` - every literal with incremental indexing |
| `00 00 04 08 00 00 00 00 01 00 9F 00 01`, twice | `WINDOW_UPDATE` stream 1 | increment 10420225, sent twice |

After the server's `SETTINGS` curl sends its ACK (`00 00 00 04 01 00 00 00 00`), and when it is
done it sends `GOAWAY` last-stream 0, `NO_ERROR`, debug data `shutdown\0`
(`00 00 11 07 00 00 00 00 00 00 00 00 00 00 00 00 00 73 68 75 74 64 6F 77 6E 00`) and closes.

**`h2` over TLS** (`-Tls -TlsAlpn h2,http/1.1`; the ClientHello read raw with `-Raw` and no `-Tls`):

| Command | Build | Server sends | Exit | ALPN agreed | Bytes and output |
| --- | --- | --- | --- | --- | --- |
| `curl -k -sS https://127.0.0.1:<port>/` (ClientHello) | S | nothing (no TLS) | 35 | - | ALPN extension `00 10 00 0E 00 0C 02 68 32 08 68 74 74 70 2F 31 2E 31`: `h2`, `http/1.1` |
| `curl -k -sS wss://127.0.0.1:<port>/` (ClientHello) | S | nothing (no TLS) | 35 | - | no ALPN extension at all |
| `curl -k -sS https://...` | S | E | 16 | `h2` | 139 bytes (preface, `SETTINGS`, `WINDOW_UPDATE`, `HEADERS`, two `WINDOW_UPDATE`s), then ACK; `curl: (16) Error in the HTTP2 framing layer` when the server closes |
| `curl -k -sS -i https://...` | S | E A R | 0 | `h2` | 165 bytes; stdout `HTTP/2 200 \r\n\r\nhi` |
| `curl -k -sS -i https://...` | W | E A R | 0 | `h2` | 165 bytes; stdout `HTTP/2 200 \r\n\r\nhi` |
| `curl -k -sS --http1.1 https://...` | S | (HTTP mode) | 52 | `http/1.1` | `GET / HTTP/1.1`: `--http1.1` offers only `http/1.1` |
| `curl -k -sS --http1.0 https://...` | S | (HTTP mode) | 52 | `http/1.1` | `GET / HTTP/1.0` |
| `curl -k -sS --http2-prior-knowledge https://...` | S | none | 16 | `h2` | the same preface and frames |
| `curl -k -sS -i -f https://...` | S | E, `HEADERS` stream 1 `08 03 35 30 33` (`:status 503`), `GOAWAY` last-stream 1 `NO_ERROR` | 22 | `h2` | stdout `HTTP/2 503`; `curl: (22) The requested URL returned error: 503` |
| `curl -k -sS https://...` | S | E, `RST_STREAM` stream 1 `HTTP_1_1_REQUIRED` (0xd) | killed at 8 s | `h2`, then `http/1.1` | curl opens a second connection offering only `http/1.1` (the recorder serves one connection, so it waits) |
| `curl -k -sS --ntlm -u a:b https://...` | S | none | 16 | `h2` | `HEADERS` (0x53 bytes) carries `authorization: NTLM TlRM...` over HTTP/2 |
| `curl -k -sS --negotiate -u a:b https://...` | S | none | 16 | `h2` | `HEADERS` (0x55 bytes) carries `authorization: Negotiate ...` over HTTP/2 |

**Prior knowledge** (`curl -sS -i --http2-prior-knowledge http://127.0.0.1:<port>/`, build S, `-Raw`):

| Server sends | Exit | curl's answer, output |
| --- | --- | --- |
| E A R | 0 | the 165 bytes above with `:scheme http`, ACK, `GOAWAY NO_ERROR "shutdown"`; stdout `HTTP/2 200 \r\n\r\nhi` |
| `SETTINGS` `MAX_CONCURRENT_STREAMS` 100 and `MAX_HEADER_LIST_SIZE` (6) 1048576 (`00 00 0C 04 00 00 00 00 00 00 03 00 00 00 64 00 06 00 10 00 00`), A, `HEADERS` stream 1 `END_STREAM` without `END_HEADERS` (`88`), `CONTINUATION` `END_HEADERS` (`00 01 78 01 79`, `x: y`) | 0 | stdout `HTTP/2 200 \r\nx: y\r\n\r\n` |
| E A, `PING` `01 02 03 04 05 06 07 08`; then `HEADERS` `88` | 0 | `PING` ACK with the same 8 bytes (`00 00 08 06 01 00 00 00 00 01 ... 08`); stdout `HTTP/2 200` |
| E A, `HEADERS` `08 03 34 33 31` (`:status 431`) | 0 | stdout `HTTP/2 431 ` |
| E A, `RST_STREAM` stream 1 `PROTOCOL_ERROR` | 92 | `curl: (92) HTTP/2 stream 1 reset by server (error 0x1 PROTOCOL_ERROR)` |
| E A, `RST_STREAM` stream 1 `ENHANCE_YOUR_CALM` | 92 | `curl: (92) HTTP/2 stream 1 reset by server (error 0xb ENHANCE_YOUR_CALM)` |
| E A, `RST_STREAM` stream 1 `REFUSED_STREAM` | killed at 8 s | curl retries the request on a new connection |
| E A, `GOAWAY` last-stream 0 `ENHANCE_YOUR_CALM` | killed at 8 s | stream 1 was not processed, so curl retries on a new connection |
| E A, `GOAWAY` last-stream 1 `ENHANCE_YOUR_CALM` | 16 | `curl: (16) Error in the HTTP2 framing layer` |
| E A, `GOAWAY` last-stream 1 `FRAME_SIZE_ERROR` | 16 | `curl: (16) Error in the HTTP2 framing layer` |
| E A (`-T` of a 2000000-byte file to `/up.bin`) | 16 when the server closes | `HEADERS` with `END_HEADERS` only: `:method PUT`, `content-length 2000000`, no `expect`; then `DATA` frames of 16384 bytes stopping at 65535 bytes, the default initial window |

**`Upgrade: h2c`** (`curl -sS -i --http2 http://127.0.0.1:<port>/`, build S):

| Server sends | Exit | Bytes and output |
| --- | --- | --- |
| `HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n` (HTTP mode) | 0 | `GET / HTTP/1.1\r\nHost: ...\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nUpgrade: h2c\r\nHTTP2-Settings: AAMAAABkAAQAAQAAAAIAAAAA\r\nConnection: Upgrade, HTTP2-Settings\r\n\r\n` (172 bytes); stdout the HTTP/1.1 head |
| `-d abc` (HTTP mode, 200) | 0 | the same `Upgrade: h2c` fields on the `POST`, with `Content-Length: 3` and the body |
| `HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: h2c\r\n\r\n` + E; then A R | 0 | after the 101 curl sends the preface, `SETTINGS`, the connection `WINDOW_UPDATE`, the ACK of E and two stream-1 `WINDOW_UPDATE`s, and no `HEADERS`: the upgraded request is stream 1. stdout `HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: h2c\r\n\r\nHTTP/2 200 \r\n\r\nhi` |

`HTTP2-Settings` is the base64url of the same 18 `SETTINGS` bytes curl sends in its preface.

## Decision

1. **Windows measures HTTP/2 with the static 8.21.0 build (S).** It is the reference release, the
   same source, OpenSSL and nghttp2 as the Linux and macOS reference builds, and it completed every
   exchange above byte for byte as W did. ADR-0017 decision 3 is amended: 8.22.0_2 stays pinned for
   HTTP/3 only; HTTP/2 on Windows uses S, whose `origin` in `UpstreamCurlBuilds.json` now names it.
   The Git for Windows reference build still measures HTTP/1.x on Windows.
2. **`https` offers `h2`, then `http/1.1`.** `TlsApplicationProtocols` for `https` becomes
   `[h2, http/1.1]`, in that order (the server's preference wins in .NET's ALPN selection, as
   measured: S and W agree `h2`). `wss` keeps `[http/1.1]`: curl 8.21.0 sends no ALPN for `wss`
   and its WebSocket upgrade is HTTP/1.1's. Every other scheme still offers none. No surl option
   turns HTTP/2 off: the client chooses, and curl's own `--http1.1` and `--http1.0` offer only
   `http/1.1` (measured), which is how a test asks for HTTP/1.x. A connection that agreed
   `http/1.1` or no ALPN is served HTTP/1.x as today. Existing `https` conformance tests: a test
   that pins HTTP/1.x output (`-i`, `-v`, `-w %{http_version}`, a response head) adds `--http1.1`
   to curl's arguments; a test that pins only the exit code and body stays as it is and so runs
   over HTTP/2 on Linux and macOS and over HTTP/1.1 on Windows' Schannel build.
3. **h2c, both ways in.**
   - *Prior knowledge.* The `http` listener (and, for `https`, a connection that agreed `h2`) reads
     the first 24 bytes: exactly `PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n` starts HTTP/2; anything else
     is read as an HTTP/1.x request head, byte for byte as today. A prefix that stops matching
     falls back with the bytes already read, so `PUT` or `POST` requests are never mistaken.
   - *`Upgrade: h2c`.* An HTTP/1.1 request with `Upgrade: h2c`, exactly one `HTTP2-Settings`
     field that decodes (base64url) to whole `SETTINGS` entries, and both named in `Connection`,
     and with no request body (no `Content-Length` above 0, no `Transfer-Encoding`), is answered
     `HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: h2c\r\n\r\n` followed at
     once by the server's `SETTINGS`; the request becomes stream 1, half-closed by the client, and
     its answer goes out as HTTP/2 frames. The settings in `HTTP2-Settings` are applied as the
     client's first `SETTINGS`, and the client's preface after the 101 is still required. A
     request with a body, or with a malformed `HTTP2-Settings`, ignores the upgrade and is answered
     HTTP/1.1 as today (RFC 9113 section 3.1 deprecates the upgrade, and RFC 7540 section 3.2 lets
     a server ignore it). `Upgrade: h2c` over TLS is ignored too (RFC 7540 section 3.2: `h2c` is
     cleartext only).
4. **Where the code lives.** HPACK (RFC 7541: the static and dynamic tables, integer and string
   literals, and the Huffman code) is a new horizontal library, `Surl.Hpack.UnitLibrary`, with
   `Surl.Hpack.UnitTests`, in `Protocols/Http/` beside `Surl.HttpMessage`, referencing nothing;
   QPACK (HTTP/3) later reuses its Huffman code and static-table machinery. ADR-0002 decision 3's
   table gains the row `Surl.Hpack.UnitLibrary` (ADR-0081) - "nothing". Frames, streams, flow
   control and the connection live in `Surl.Protocol.Http.UnitLibrary`, which references
   `Surl.Hpack`. No contract in `Surl.Protocol.Abstractions` changes: ALPN already reaches the
   server as `TlsSession.ApplicationProtocol`.
5. **`SETTINGS` and the ADR-0006 limits.** The server's first frame is `SETTINGS` with
   `MAX_CONCURRENT_STREAMS` = 100 and `MAX_HEADER_LIST_SIZE` = `--max-message` (1048576 by
   default); `HEADER_TABLE_SIZE` (4096), `INITIAL_WINDOW_SIZE` (65535) and `MAX_FRAME_SIZE` (16384)
   stay at RFC 9113's defaults and are not sent; `ENABLE_PUSH` is a client setting and is not sent.
   Curl accepted both an empty `SETTINGS` and this one (measured). The server ACKs every client
   `SETTINGS`, and replenishes the connection and stream receive windows with `WINDOW_UPDATE` as
   it consumes `DATA`, so a request body never stalls on the server's window.

   | Limit (ADR-0006 section 1) | HTTP/2 | Ends with |
   | --- | --- | --- |
   | Too many connections | The refusal reads, within `RefusalWriteDeadline`, up to the client's first `HEADERS`, then sends `SETTINGS`, `HEADERS` `:status 503` with `END_STREAM`, and `GOAWAY` naming that stream, `NO_ERROR`; no `HEADERS` in time: close with no bytes | curl's 503 (22 with `-f`), measured |
   | Head timeout | A preface not complete within `--head-timeout`: close with no bytes. A header block (`HEADERS` to `END_HEADERS`) not complete within it | `GOAWAY` `ENHANCE_YOUR_CALM`, then close |
   | Maximum framed message (`--max-message`) | A header block, `HEADERS` and its `CONTINUATION`s together, longer than the limit; a `HEADERS` or `CONTINUATION` is never left half-decoded, since HPACK state is shared by the whole connection | `GOAWAY` `ENHANCE_YOUR_CALM`, last-stream the offending stream, then close (curl 16, measured) |
   | Frame size | A frame longer than `MAX_FRAME_SIZE` (16384) | `GOAWAY` `FRAME_SIZE_ERROR`, then close |
   | Concurrent streams | A `HEADERS` opening a 101st open stream | `RST_STREAM` `REFUSED_STREAM` on it; the connection carries on (curl retries it, measured) |
   | Flow-control windows | `DATA` past the window the server granted | `GOAWAY` `FLOW_CONTROL_ERROR`, then close |
   | Maximum upload (`--max-filesize`) | A `content-length` over the limit, or `DATA` past it | `HEADERS` `:status 413` with `END_STREAM`, then `RST_STREAM` `NO_ERROR` if the client's half is still open (RFC 9113 section 8.1); the partial upload is deleted |
   | Idle timeout, maximum duration | No frame either way for `--idle-timeout`; `--max-time` reached | `GOAWAY` naming the last stream processed, `NO_ERROR`, then close |
   | Shutdown (ADR-0059) | The listener stops | `GOAWAY` naming the last stream processed, `NO_ERROR`; open streams finish within the shutdown grace, then close |

   A `GOAWAY` for a limit always names the highest stream the server has received (the offending
   one included): measured, a `GOAWAY` with last-stream 0 makes curl retry the request on a new
   connection, which for a limit is a loop. Protocol errors that are not limits follow RFC 9113:
   a malformed request (a missing or repeated pseudo-header, an uppercase field name, a
   connection-specific field) is `RST_STREAM` `PROTOCOL_ERROR` (curl 92, measured); an HPACK
   decoding failure is `GOAWAY` `COMPRESSION_ERROR`; any other connection error is `GOAWAY`
   `PROTOCOL_ERROR`. Each one is the protocol's own error, then close, as ADR-0006 section 5's
   HTTP/2 cell says; its idle-timeout row's "close with no bytes" for HTTP becomes the `GOAWAY`
   above on an HTTP/2 connection.
6. **What curl sends and needs.** Curl sends the preface, its `SETTINGS`, a connection
   `WINDOW_UPDATE` and its first `HEADERS` without waiting for the server's `SETTINGS`; the server
   must read them in that order and must not require its own `SETTINGS` to be ACKed first. Curl
   compresses with Huffman literals and incremental indexing, so the decoder keeps the dynamic
   table. Curl sends no `PRIORITY` frames and no priority fields in `HEADERS`; the server still
   parses and ignores both (RFC 9113 section 5.3.2). The server answers each `PING` with an ACK of
   the same 8 bytes and sends none of its own. The server's response header blocks go in one
   `HEADERS` frame, followed by `CONTINUATION`s when longer than 16384 bytes (curl took a split
   block, measured), and its `DATA` frames are at most 16384 bytes and never exceed curl's
   windows. Server push stays off: curl sends `ENABLE_PUSH` 0, and the server never sends
   `PUSH_PROMISE`. Curl's own end is `GOAWAY` `NO_ERROR` then close, which ends the connection
   quietly.
7. **What carries over.**
   - *Content store, methods, statuses, ranges, conditional requests and the log lines* carry
     over unchanged through the answering shape in decision 8; a log line's version reads
     `HTTP/2`.
   - *Uploads.* A body is the stream's `DATA`; `content-length`, when sent, must match it or the
     stream is `RST_STREAM` `PROTOCOL_ERROR`. `--max-filesize` as in decision 5.
   - *`100 Continue`.* Curl sends no `Expect` over HTTP/2 (measured), and flow control does what
     `100 Continue` does on HTTP/1.1; an `expect: 100-continue` that does arrive is answered with
     an interim `HEADERS` `:status 100` before the body is read, as ADR-0027 answers HTTP/1.1.
   - *Logins bound to a request* - Basic, Digest, Bearer, AWS Signature Version 4, and the body
     check of ADR-0045 - carry over per stream.
   - *Logins bound to a connection* - NTLM (ADR-0041) and Negotiate (ADR-0044) - are not allowed
     over HTTP/2 (RFC 9113 section 8.2.3; RFC 7540 section 13.1 reserves `HTTP_1_1_REQUIRED` for
     this). Curl 8.21.0 sends `Authorization: NTLM` and `Negotiate` over HTTP/2 (measured). A
     stream whose `authorization` names NTLM or Negotiate, or whose only acceptable challenge for
     the account would be one of them, is answered `RST_STREAM` `HTTP_1_1_REQUIRED`; curl then
     opens a new connection offering only `http/1.1` (measured) and the login runs over HTTP/1.1
     as today. On prior knowledge there is no ALPN to fall back with, so the same reset ends that
     transfer; that is curl's choice and is pinned as measured when BL-371 runs it.
   - *The lingering close* (ADR-0021) applies to an HTTP/2 connection as to every TCP connection:
     after its `GOAWAY` the server keeps reading, and discarding, until the client closes or the
     lingering deadline passes, so a client's last frames never meet a reset. Drain-before-close
     (ADR-0024) is HTTP/1.x's: on HTTP/2 an unread body is ended per stream with `RST_STREAM`
     `NO_ERROR` after the answer, and the connection carries on.
8. **The answering shape BL-366 splits out.** `HttpRequestResponder` is split into:
   - `HttpRequestAnswerer` - decides, never writes: given an `HttpRequest` (method, target,
     authority, fields, and an `IHttpRequestBody` it may read or discard), it returns an
     `HttpAnswer` - status, response fields, and an `HttpAnswerBody` (none, bytes, or a
     content-store file with its length and range) - plus whether the request's body was left
     unread. It holds the content store, the authentication session and the log, and never asks
     which HTTP version it is answering.
   - `Http1AnswerWriter` - turns an `HttpAnswer` into exactly the HTTP/1.x bytes written today,
     owning `Connection: close` and keep-alive, chunked framing, `100 Continue`, drain-before-close
     and the lingering close.
   - BL-368's `Http2StreamAnswerWriter` - turns the same `HttpAnswer` into `HEADERS`,
     `CONTINUATION` and `DATA` frames on one stream; BL-383's HTTP/3 writer does the same on a
     QUIC stream.
9. **WebSocket over HTTP/2 (RFC 8441): no.** Curl 8.21.0 sends no ALPN for `wss` and upgrades
   `ws` and `wss` with an HTTP/1.1 `Upgrade: websocket` (measured), so it never sends an extended
   `CONNECT`. The server does not send `SETTINGS_ENABLE_CONNECT_PROTOCOL`, and a `CONNECT` with a
   `:protocol` pseudo-header is `RST_STREAM` `PROTOCOL_ERROR`. `Surl.Protocol.Ws` is unchanged.

## Alternatives considered

- **8.22.0_2 for HTTP/2 on Windows, as ADR-0017 planned.** Rejected: S is the reference release
  and shares OpenSSL and nghttp2 with the Linux and macOS reference builds, so all three platforms
  measure HTTP/2 against the same curl.
- **A surl option to turn HTTP/2 off.** Rejected: curl already offers the switch on its side
  (`--http1.1`), and an option nobody needs is surface to document and test.
- **Refusing every connection-bound login over HTTP/2 with `401`.** Rejected: curl would stop;
  `HTTP_1_1_REQUIRED` is what the specification provides and what curl acts on.
- **HPACK inside `Surl.Protocol.Http`.** Rejected: QPACK reuses its Huffman code, and the HTTP/3
  server must not reference the HTTP/2 one's internals; a horizontal library keeps both clean.

## Consequences

Good:

- Every byte the HTTP/2 tasks pin about curl's side was measured from a pinned build first.
- The `https` listener serves HTTP/2 to every curl that asks, and HTTP/1.1 to every other client,
  with no option.

Costs and caveats:

- The Linux and macOS conformance legs switch to HTTP/2 for `https` the moment BL-370 lands, so
  that task must adapt the tests decision 2 names in the same diff.
- The recorder serves one `-Raw` connection, so curl's retry after `REFUSED_STREAM`,
  `GOAWAY` last-stream 0 and `HTTP_1_1_REQUIRED` is recorded only as far as the second
  connection's ALPN; BL-371 pins the full retries against a live surl.
- Upload refusal (`413` then `RST_STREAM NO_ERROR`), `FRAME_SIZE_ERROR`, `FLOW_CONTROL_ERROR` and
  the idle `GOAWAY` follow RFC 9113 and are not yet run against curl; BL-371 measures curl's exit
  code for each and corrects this ADR by amendment if curl differs.
