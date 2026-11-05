# ADR-0117 — How the WebTransport server answers `webtransport`

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05,
  in BL-457. BL-458 builds the library with its capsule and stream-header codec (decisions 4 and 6),
  BL-459 registers with `Surl.Http3`, establishes sessions and echoes streams and datagrams
  (decisions 0 to 3 and 6), BL-460 applies flow control, the session limit, authentication and the
  limits (decisions 4, 5, 7 and 8), and BL-745 gives `Surl.Quic` the `RESET_STREAM_AT` frame
  decision 0 needs.
- **Builds on:** [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
  (the oracle is the specification; the Curl port is never proof; default ports; one matrix per
  server), [ADR-0086](ADR-0086-how-the-extended-servers-share-http-2-http-3-and-quic.md) decisions 3
  and 4 (`Surl.Http3`'s `IHttp3ExtendedConnectHandler`, claimed stream types, HTTP datagrams, QUIC
  `DATAGRAM` frames; the WebTransport settings and the treatment of a stream naming no open session
  are this ADR's), [ADR-0082](ADR-0082-how-surl-serves-http-3-over-quic.md) decisions 7 and 9 (the
  QUIC and HTTP/3 limits; request-bound and connection-bound logins over HTTP/3),
  [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) sections 4, 6, 8
  and 10 (HTTP policy, `IHttpAuthenticationSession`, the refusal delay, `--self-signed`),
  [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  decision 4 (one account set; a Bearer token is an empty-name account's password),
  [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) (limits and fixed error text) and
  [ADR-0059](ADR-0059-how-a-protocol-server-tells-a-limit-from-shutdown.md) (shutdown grace).
- **Amends:** nothing.

## Context

`Surl.Extended.Protocol.WebTransport` (in `Protocols/Http/`, ADR-0083's table) answers the
`webtransport` scheme the Curl port adds, on UDP port 443 when the listen URL names none
(ADR-0083 decision 7). By ADR-0083 decision 1 it is judged by its specification, not by upstream
curl, which has no WebTransport, and not by the Curl port. The specifications read (2026-11-05) are:

- **WebTransport over HTTP/3, draft-ietf-webtrans-http3-16** (6 July 2026; below "WTH3"): section
  3.1 (a WebTransport-capable connection: `SETTINGS_WT_ENABLED`, `SETTINGS_ENABLE_CONNECT_PROTOCOL`,
  `SETTINGS_H3_DATAGRAM`, `max_datagram_frame_size`, `reset_stream_at`), 3.2 (the extended CONNECT,
  `Origin`, 405 and 403), 3.3 (`WT-Available-Protocols` and `WT-Protocol`), 4 (session IDs), 4.2
  and 4.3 (stream type `0x54`, signal `0x41`), 4.4 (`RESET_STREAM_AT` and the error-code mapping),
  4.5 (datagrams), 4.6 (buffering), 4.7 (`GOAWAY` and `WT_DRAIN_SESSION`), 5 (flow control: 5.1
  negotiation, 5.2 the session limit, 5.3 and 5.4 stream and data limits, 5.5 the three
  `SETTINGS_WT_INITIAL_*`, 5.6 the capsules), 6 (session termination, `WT_CLOSE_SESSION`), 9 (the
  codepoints);
- **RFC 9220** (extended CONNECT over HTTP/3), **RFC 9297** (HTTP datagrams and the Capsule
  Protocol), **RFC 9221** (QUIC `DATAGRAM` frames), and **draft-ietf-quic-reliable-stream-reset**
  at the revision WTH3 cites (`RESET_STREAM_AT`).

**The revision is pinned at -16, and -17 is not taken.** draft-ietf-webtrans-http3-17 appeared on
2026-11-02. Against -16 it changes `SETTINGS_WT_ENABLED`'s codepoint (`0x2c7cf000` becomes
`0x20b8933b`), reworks draft-version negotiation (section 7.1: a server reads the client's
`SETTINGS_WT_ENABLED` codepoints and picks a version), makes a misplaced `WT_STREAM` an
`H3_FRAME_UNEXPECTED`, drops the 0-RTT rule on reducing flow-control settings, and tightens the
`WT_MAX_*` increase rule; the `:protocol` token, the capsule types and the other error codes are
unchanged. -16 is kept because the only client Surl is tested against speaks -16 (the Curl port's
ADR-1089), a -17 server would not even recognise that client's `SETTINGS_WT_ENABLED`, and -17 is
three days old. -17's own section 7.1 lets one server offer two drafts by codepoint, so a later ADR
can add -17 beside -16 without breaking -16 clients; this ADR's behaviour already satisfies -17's
stricter `WT_MAX_*` rule (decision 4).

**`SETTINGS_WT_MAX_SESSIONS` no longer exists.** Earlier drafts limited sessions with it; -16 has
no such setting (section 9.2 registers only `SETTINGS_WT_ENABLED` and the three
`SETTINGS_WT_INITIAL_*`), and section 5.2 limits sessions by refusing them instead. The task's
planning name is answered by decision 5.

The port's `Curl.Extended.Protocol.WebTransport.UnitLibrary` (ADR-1089) and its client matrix
`WebTransport.md` were read only to learn what a client sends, so the server accepts it: QUIC with
ALPN `h3`; `SETTINGS_ENABLE_CONNECT_PROTOCOL`, `SETTINGS_H3_DATAGRAM` and `SETTINGS_WT_ENABLED`
(`0x2c7cf000`) = 1, and no `SETTINGS_WT_INITIAL_*` (so flow control stays off, decision 4); one
extended CONNECT with `:protocol webtransport-h3`, `:scheme https`, `:authority` and `:path`; then
one client bidirectional stream (`0x41`), one client unidirectional stream (`0x54`) or one HTTP
datagram; it reads the echo, the first server unidirectional stream, or the first datagram; then
`WT_CLOSE_SESSION` code 0 and FIN. Nothing below is taken from the port's output, and no expected
byte comes from it.

## Decision

### 0. What the server is, and the connection it offers (WTH3 3.1)

- **One WebTransport server** on HTTP/3. `Surl.Extended.Protocol.WebTransport` references
  `Surl.Protocol.Abstractions` and `Surl.Http3` (ADR-0086 decision 3), never another protocol
  server. It registers one `IHttp3ExtendedConnectHandler` with `Protocol` `webtransport-h3`,
  `BidirectionalStreamSignal` `0x41` and `UnidirectionalStreamType` `0x54`, and one request-stream
  handler for every other request (decision 1). `Surl.Http3` and `Surl.Quic` own QUIC, TLS 1.3,
  QPACK, frames, `GOAWAY` and the ADR-0082 limits; the WebTransport server sees sessions, their
  streams, their capsules and their datagrams. Time comes from `ExchangeContext.TimeProvider`.
- **TLS**: QUIC is always TLS 1.3 (ADR-0082), ALPN `h3` only (ADR-0086 decision 4). `--cert` or
  `--self-signed` is required, `CertificateProblem` (58) without, as for every TLS listener.
- **SETTINGS sent** (WTH3 3.1 and 5.5, section 9.2's codepoints): `SETTINGS_ENABLE_CONNECT_PROTOCOL`
  (`0x08`) = 1 and `SETTINGS_H3_DATAGRAM` (`0x33`) = 1 (`Surl.Http3` adds both once a handler is
  registered), `SETTINGS_WT_ENABLED` (`0x2c7cf000`) = 1, `SETTINGS_WT_INITIAL_MAX_STREAMS_UNI`
  (`0x2b64`) = 100, `SETTINGS_WT_INITIAL_MAX_STREAMS_BIDI` (`0x2b65`) = 100 and
  `SETTINGS_WT_INITIAL_MAX_DATA` (`0x2b61`) = 1048576, all carried by `Http3ServerOptions`'
  further parameters (ADR-0086 decision 3).
- **Transport parameters sent** (WTH3 3.1): `max_datagram_frame_size` = 65535 (RFC 9221 section 3,
  BL-573) and an empty `reset_stream_at` (BL-745).
- **The client's SETTINGS** (WTH3 3.1, "MUST NOT process incoming WebTransport requests until the
  client's SETTINGS"): an extended CONNECT that arrives before the client's `SETTINGS` waits for
  them, within `--head-timeout`. A client whose `SETTINGS` lack `SETTINGS_H3_DATAGRAM` = 1 or
  `SETTINGS_WT_ENABLED` = 1 (section 3.1: draft clients MUST send it), or whose transport
  parameters lack a non-zero `max_datagram_frame_size`, has its connection closed with
  `WT_REQUIREMENTS_NOT_MET` (`0x212c0d48`) when it first sends a WebTransport CONNECT, stream or
  datagram (section 3.1 treats the session as malformed; section 9.5's code says why). A client
  that never asks for WebTransport is never closed for its settings. Requiring
  `SETTINGS_WT_ENABLED` is how the server knows the client speaks -16 and not a draft whose
  codepoints differ.
- **Resets of WebTransport data streams** use `RESET_STREAM_AT` with a Reliable Size equal to the
  stream header the server wrote on that half (`0x54` and the session ID on its unidirectional
  streams; nothing on the server half of a client bidirectional stream, so 0), as WTH3 4.4 requires.
  Application error codes the server sends are its own 32-bit codes mapped into
  `WT_APPLICATION_ERROR`'s range by section 4.4's formula
  (`0x52e4a40fa8db + n + floor(n / 0x1e)`); codes received are mapped back the same way.
- **Session IDs** (WTH3 4): a stream header or datagram whose session ID is not a client-initiated
  bidirectional stream ID closes the connection with `H3_ID_ERROR` (`0x108`). A quarter stream ID
  RFC 9297 section 2.1 rejects is `Surl.Http3`'s to answer (BL-411).

### 1. The paths that accept a session (WTH3 3.2)

The server answers requests in this order, the first failure ending the request:

| Check | Answer | Why |
| --- | --- | --- |
| a well-formed extended CONNECT: `:protocol`, `:scheme`, `:authority` and `:path` all present | stream error `H3_MESSAGE_ERROR` (`0x10e`) | RFC 9220 section 3 and RFC 9114 section 4.3.1 make it malformed; `Surl.Http3` answers it (BL-411) |
| `:protocol` is `webtransport-h3` | as RFC 9220 says for an unregistered protocol (`Surl.Http3`, BL-411) | WTH3 3.2: "MUST be set to webtransport-h3"; no other protocol is registered |
| not an extended CONNECT at all (`GET`, a plain `CONNECT`, anything else) | `:status 405`, `allow: CONNECT`, no body, FIN | the listener serves no HTTP resource; RFC 9110 section 15.5.6 requires `allow` |
| `:scheme` is `https` | `:status 400`, no body, FIN | WTH3 3.2: "MUST be https"; the request is well formed HTTP but not a WebTransport request |
| `origin`, when present, is the server's own origin | `:status 403`, no body, FIN | WTH3 3.2: the server MUST verify `Origin` and SHOULD answer 403 when it fails |
| authentication (decision 7) | `:status 401` or `403` | decision 7 |
| `:path` is the listen URL's path or below it | `:status 405`, `allow: CONNECT`, no body, FIN | WTH3 3.2: a resource that does not support WebTransport SHOULD get 405; judged after authentication, so an unauthenticated client learns nothing about the paths served |
| the session limit (decision 5) | the CONNECT stream reset `H3_REQUEST_REJECTED` (`0x10b`) | decision 5 |
| otherwise | `:status 200`, no other field, no FIN: the session is established | WTH3 3.2: a 2xx accepts the session |

- **Paths.** The listen URL's path, compared byte for byte without its query, and every path below
  it at a `/` boundary; a query on `:path` is ignored. A listen URL with no path (or `/`) accepts
  every path. `webtransport://0.0.0.0:4433/echo` accepts `/echo` and `/echo/a`, not `/echoes`.
- **Origin.** "The server's own origin" is `https://` followed by `:authority`, the host compared
  case-insensitively and a `:443` port treated as absent. An absent `origin` is accepted (WTH3 3.2:
  it is optional for clients that are not browsers). `null` and every other origin are refused. No
  option lists other origins: a browser test page served from another origin is the only case it
  would serve, and no client Surl is tested with is one; a later ADR may add one.
- **Capsules are never read optimistically** (WTH3 3.2): the response is sent before the server
  reads anything after the CONNECT's header section. Streams and datagrams that arrive for the
  CONNECT stream before the answer are held as decision 3 says.

### 2. Subprotocol negotiation (WTH3 3.3)

The echo service speaks no application protocol, so the server never sends `WT-Protocol`, and a
CONNECT is accepted with or without `WT-Available-Protocols`. WTH3 3.3 makes `WT-Protocol` a
server's MAY and lets a server that needs no negotiation ignore the request field; choosing one of
the client's names would claim the server speaks it, which it does not. A malformed
`WT-Available-Protocols` (not a Structured Field List of Strings) is ignored, as section 3.3 says.
A client that requires negotiation closes the session with `WT_ALPN_ERROR` itself; that is the
answer a server with no protocols should get.

### 3. What the server does with data (WTH3 4.2, 4.3, 4.5, 4.6)

- **Bidirectional streams.** Each client bidirectional stream of a session (signal `0x41`, the
  session ID) has every byte after its header written back on the same stream, in order, as it
  arrives; the client's FIN is answered by FIN. The server opens no bidirectional stream of its own.
- **Unidirectional streams.** For each client unidirectional stream (type `0x54`, the session ID),
  the server opens one unidirectional stream of its own with `0x54` and the same session ID, writes
  every byte after the client's header to it as it arrives, and finishes it when the client's
  stream finishes. The server opens no other unidirectional stream.
- **Datagrams.** Each HTTP datagram for an established session (QUIC `DATAGRAM`, the quarter stream
  ID of the CONNECT stream, WTH3 4.5) is sent back with the same quarter stream ID and the same
  payload. A `DATAGRAM` capsule (RFC 9297 section 3.5, type `0x00`) on the CONNECT stream is echoed
  as a `DATAGRAM` capsule. A datagram the client's `max_datagram_frame_size` cannot carry is dropped
  (RFC 9221 section 5: datagrams are unreliable).
- **Resets.** A client `RESET_STREAM` or `RESET_STREAM_AT` on a stream's sending half makes the
  server reset the half it writes for that stream (the same bidirectional stream, or its answering
  unidirectional stream) with the same error code, after writing every byte it already echoed up to
  the client's Reliable Size. A client `STOP_SENDING` on the server's half makes the server reset
  that half with the same code and send `STOP_SENDING` with that code on the client's half. Codes are
  mirrored as received, so a client sees its own application code back (WTH3 4.4).
- **Streams and datagrams for no open session** (WTH3 4.6):
  - for a session ID whose CONNECT has not arrived or not been answered: held, up to 16 streams and
    64 datagrams a connection, until the session is established (then served) or refused, or until
    `--head-timeout` passes from the stream's or datagram's arrival. A stream past the 16, or one
    whose session is refused or times out, gets `RESET_STREAM` and `STOP_SENDING`
    `WT_BUFFERED_STREAM_REJECTED` (`0x3994bd84`); a datagram past the 64, or whose session is
    refused or times out, is dropped. A held stream is not read beyond its header;
  - for a session ID naming a request stream that is not a session (an ordinary request, or a
    refused CONNECT): reset at once with `WT_BUFFERED_STREAM_REJECTED`; datagrams dropped;
  - for a session that has ended: reset at once with `WT_SESSION_GONE` (`0x170d7b68`); datagrams
    dropped.
- The first bytes of a client bidirectional stream that are `0x41` arrive only as WTH3 4.3 allows;
  `Surl.Http3` answers `0x41` anywhere else as `H3_FRAME_ERROR` (`0x106`, BL-411).

### 4. Flow control (WTH3 5)

- **The server always offers flow control**: its `SETTINGS` carry the three non-zero
  `SETTINGS_WT_INITIAL_*` of decision 0. By WTH3 5.1 flow control is on for a connection only when
  the client also sends one of them non-zero. Initial limits per session: 100 client bidirectional
  streams, 100 client unidirectional streams, 1048576 bytes of session data.
- **With flow control on**, the server:
  - counts every client stream of a session against its `WT_MAX_STREAMS`, and every byte of stream
    payload the client sends on the session's streams (headers excluded, WTH3 5.4) against its
    `WT_MAX_DATA`;
  - sends `WT_MAX_STREAMS` (bidirectional `0x190b4d3f`, unidirectional `0x190b4d40`) raising the
    limit by the streams closed once half the current credit is used, and `WT_MAX_DATA`
    (`0x190b4d3d`) raising it to the bytes consumed plus 1048576 once half the window is consumed
    (WTH3 5.6: "SHOULD send ... as they consume data", never waiting for a `*_BLOCKED`). Each value
    sent is above the last (and above the initial setting), which -17 also requires. `WT_MAX_DATA`
    is never raised past `--max-filesize` (decision 8);
  - honours the client's limits for what it sends: its echoed bytes count against the client's
    `WT_MAX_DATA` and its unidirectional answers against the client's unidirectional
    `WT_MAX_STREAMS`, starting from the client's `SETTINGS_WT_INITIAL_*`. When blocked it stops
    reading the client stream it would echo (so QUIC flow control holds the client) and sends
    `WT_DATA_BLOCKED` (`0x190b4d41`) or `WT_STREAMS_BLOCKED` (bidirectional `0x190b4d43`,
    unidirectional `0x190b4d44`) once for each limit value it is blocked at;
  - reads the client's `WT_DATA_BLOCKED` and `WT_STREAMS_BLOCKED` only to log them.
- **Violations** (WTH3 5.6.2, 5.6.4) end the session with `WT_FLOW_CONTROL_ERROR` (`0x045d4487`): a
  client stream past `WT_MAX_STREAMS`, stream payload past `WT_MAX_DATA`, a client `WT_MAX_DATA` or
  `WT_MAX_STREAMS` that does not increase the last value, or a `WT_MAX_STREAMS` above 2^60. Ending a
  session with an HTTP/3 error code is resetting its CONNECT stream (`RESET_STREAM` and
  `STOP_SENDING` with that code), then decision 6's teardown of its streams.
- **`WT_MAX_STREAM_DATA` and `WT_STREAM_DATA_BLOCKED`** are prohibited over HTTP/3 (WTH3 5.4: a
  "session error"); either ends the session as above with `H3_MESSAGE_ERROR` (`0x10e`), the code for
  a request stream carrying what it must not. BL-458 takes their type values from the section of
  draft-ietf-webtrans-http2 that WTH3 5.4 cites.
- **With flow control off**, every flow-control capsule is ignored (WTH3 5.1: "MUST ignore"), and
  only QUIC's own stream and data limits (ADR-0082 decision 7) apply.

### 5. The session limit (WTH3 5.1, 5.2; no `SETTINGS_WT_MAX_SESSIONS`)

- **With flow control off**: one session at a time per connection. A CONNECT that would make a
  second has its stream reset `H3_REQUEST_REJECTED` (`0x10b`), as WTH3 5.1 requires ("MUST reset
  ... with H3_REQUEST_REJECTED").
- **With flow control on**: 16 sessions at a time per connection, the next CONNECT reset
  `H3_REQUEST_REJECTED`. WTH3 5.2 offers that code or `:status 429`; `H3_REQUEST_REJECTED` tells the
  client the request was never processed, so it may retry after a session ends, and it is the same
  answer in both modes. A session counts from its `200` until decision 6's teardown ends it.
- The session limit is the rate limit WTH3 5.2 asks for: with ADR-0082's connection limits it bounds
  the sessions one client holds, and sessions are never refused by count over time.

### 6. Closing, draining and reset codes (WTH3 4.7, 6)

- **The client's `WT_CLOSE_SESSION`** (`0x2843`: a 32-bit code, then a UTF-8 message): the server
  logs the code and message, ends every stream of the session (`RESET_STREAM_AT` on the server's
  halves and `STOP_SENDING` on the client's, both `WT_SESSION_GONE`), drops its datagrams, and
  finishes its side of the CONNECT stream with FIN and no capsule of its own (WTH3 6: "MUST close
  or reset the CONNECT stream in response"). A message longer than 1024 bytes or not valid UTF-8,
  and any byte after the capsule on the CONNECT stream, resets the CONNECT stream with
  `H3_MESSAGE_ERROR` (WTH3 6). The text's 1024 is taken over the figure's 8192: the text is the
  normative MUST.
- **The client's FIN on the CONNECT stream** is a `WT_CLOSE_SESSION` with code 0 and no message
  (WTH3 6); **its reset of the CONNECT stream** ends the session the same way, the server resetting
  its own half `WT_SESSION_GONE` if still open.
- **The server's own `WT_CLOSE_SESSION`** carries a fixed ASCII message and is followed at once by
  FIN on the CONNECT stream (WTH3 6: "MUST immediately send a FIN"), then the same teardown. The
  server never opens a stream or sends a datagram for an ended session (WTH3 6). Its codes:

  | Code | Message | When |
  | --- | --- | --- |
  | 0 | `server shutting down` | shutdown or `--max-time`, once the grace has passed |
  | 1 | `session data limit reached` | the session's received payload reached `--max-filesize` (decision 8) |

- **The client's `WT_DRAIN_SESSION`** (`0x78ae`) is logged; the server keeps serving the session
  (WTH3 4.7: the session may still be used).
- **Shutdown and `--max-time`** (WTH3 4.7, ADR-0059): the server sends `WT_DRAIN_SESSION` on every
  session's CONNECT stream, then `Surl.Http3` sends `GOAWAY` (ADR-0082 decision 7). Sessions are
  still served during the shutdown grace; any still open when it ends get `WT_CLOSE_SESSION` code 0.
  The server then waits, within the grace, for the client to close each CONNECT stream (WTH3 6:
  "SHOULD wait") before `CONNECTION_CLOSE` `H3_NO_ERROR`.
- **`--idle-timeout`** discards the connection silently (ADR-0082 decision 7); its sessions end
  with it.
- **Reset codes used**, in one place: `WT_SESSION_GONE` for a session's streams when it ends and for
  streams of an ended session; `WT_BUFFERED_STREAM_REJECTED` for held or orphaned streams (decision
  3); the client's own code mirrored on an echo (decision 3); `WT_FLOW_CONTROL_ERROR` and
  `H3_MESSAGE_ERROR` on a CONNECT stream (decisions 4 and 6); `H3_REQUEST_REJECTED` for a CONNECT
  over the limit (decision 5); `H3_ID_ERROR` and `WT_REQUIREMENTS_NOT_MET` for the connection
  (decision 0).

### 7. Authentication (ADR-0032; ADR-0100 decision 4)

- **Secure by default.** Every WebTransport CONNECT is judged through the connection's
  `IHttpAuthenticationSession` (`StartHttpConnection`, ADR-0032 section 6) with
  `HttpAuthenticationRequest("CONNECT", <:path>, IsWrite: false, <regular fields>)`, reading the
  one account set of `--user`, `--user-file` and the layer-2 options (ADR-0100 decision 4), never a
  private credential source. The `authorization` field carries `Basic` (an account), `Bearer` (an
  empty-name account whose password is the token) or a request-bound scheme the session offers over
  HTTP/3 (ADR-0082 decision 9, Digest as ADR-0032 section 4 says).
- **Verdicts:** `Proceed` continues to the path check. `Challenge` is `:status 401` with the
  verdict's `www-authenticate` values, no body, FIN; the client may retry with a new CONNECT.
  `Forbidden` is `:status 403`, no body, FIN. Neither names the account or the cause, and each
  waits the session's refusal delay (ADR-0032 section 8).
- **NTLM and Negotiate** are bound to a connection, and WebTransport has no HTTP/1.1 to fall back
  to, so an `authorization` with either is answered `401` with the request-bound challenges, not
  ADR-0082's `H3_VERSION_FALLBACK`.
- **`--allow-anonymous`** serves a CONNECT without `authorization` as the anonymous caller; one that
  carries credentials is still judged, and a bad one is never served as anonymous. QUIC is always
  encrypted, so `--allow-plaintext-auth` has nothing to loosen here.
- Authorization is all or nothing: the echo service touches no store, so `403` comes only from the
  session.

### 8. Limits (ADR-0006)

| Limit | How the server applies it | Answer when hit |
| --- | --- | --- |
| connections (`--max-connections`, `--max-connections-per-address`) | ADR-0082 decision 7's refusal | `CONNECTION_CLOSE` `CONNECTION_REFUSED` |
| handshake, CONNECT header section (`--head-timeout`, `--max-message`) | `Surl.Http3` (ADR-0082 decision 7) | its answer (`CONNECTION_CLOSE`, `H3_REQUEST_INCOMPLETE`, `:status 431`) |
| client SETTINGS before a CONNECT is judged (`--head-timeout`) | decision 0 | the CONNECT stream reset `H3_REQUEST_INCOMPLETE` (`0x10d`) |
| sessions a connection | 1 without flow control, 16 with (decision 5) | `H3_REQUEST_REJECTED` |
| streams a session | flow control on: `WT_MAX_STREAMS`, 100 each way, renewed (decision 4); off: QUIC's 100 (ADR-0082) | `WT_FLOW_CONTROL_ERROR`, or QUIC's `STREAM_LIMIT_ERROR` |
| session data in flight | flow control on: `WT_MAX_DATA`, a 1048576-byte window (decision 4); off: QUIC's windows | `WT_FLOW_CONTROL_ERROR`, or QUIC's `FLOW_CONTROL_ERROR` |
| a session's received payload in total (`--max-filesize`, 104857600) | counted across its streams and datagrams | `WT_CLOSE_SESSION` code 1, `session data limit reached` |
| echo not yet written | 65536 bytes a stream; the server stops reading the stream until it drains | none: QUIC flow control holds the client |
| datagrams waiting to be echoed | 64 a session | the newest is dropped |
| datagram size | `max_datagram_frame_size` 65535 sent; a larger `DATAGRAM` frame is RFC 9221 section 3's `PROTOCOL_VIOLATION` (`Surl.Quic`, BL-573) | `CONNECTION_CLOSE` |
| a capsule (`--max-message`, 1048576) | its declared length, judged before its value is read | the CONNECT stream reset `H3_EXCESSIVE_LOAD` (`0x107`) |
| held streams and datagrams for no open session | 16 streams and 64 datagrams a connection, each for at most `--head-timeout` (decision 3) | `WT_BUFFERED_STREAM_REJECTED`, or dropped |
| `--idle-timeout` (120 s), `--max-time` (3600 s), shutdown | ADR-0082 decision 7 and decision 6 above | silent discard; `WT_DRAIN_SESSION`, `GOAWAY`, `WT_CLOSE_SESSION` code 0 |

No new option is added: ADR-0006's options cover what a test harness tunes.

## Alternatives considered

- **Pin -17.** Rejected for now (see Context): it changes `SETTINGS_WT_ENABLED`'s codepoint, so it
  would refuse the only client Surl is tested with, and it is three days old. -17's version
  negotiation lets a later ADR add it beside -16.
- **Keep a `SETTINGS_WT_MAX_SESSIONS`** from earlier drafts. Rejected: -16 has no such setting, and
  an unknown setting tells a -16 client nothing; WTH3 5.1 and 5.2's refusal is the -16 mechanism.
- **Answer every `:path`.** Rejected: the listen URL names a path, and WTH3 3.2's 405 for a resource
  without WebTransport gives a client something to test; a listen URL without a path still accepts
  every path.
- **Choose the client's first `WT-Available-Protocols` entry.** Rejected: it would claim a protocol
  the echo service does not speak, and the name would then say something false.
- **Answer the session limit with `429`.** Rejected: `H3_REQUEST_REJECTED` is required without flow
  control, and one answer in both modes is simpler; it also tells the client nothing was processed.
- **Echo a unidirectional stream only after the client finishes it.** Rejected: echoing as bytes
  arrive needs no buffer beyond decision 8's 64 KiB and matches the bidirectional echo.
- **Offer no flow control**, so only one session per connection. Rejected: the server would then
  never exercise WTH3 5, and clients that pool sessions could not be tested.
- **An option listing accepted origins.** Rejected for now: no client Surl is tested with is a
  browser on another origin.

## Consequences

- `surl webtransport://127.0.0.1:4433/` (with `--self-signed`, and `--allow-anonymous` or an
  account) is a WebTransport server that echoes bidirectional streams, unidirectional streams and
  datagrams, with flow control when the client offers it, Basic, Bearer and Digest logins, and
  draining on shutdown.
- `Surl.Quic` gains `RESET_STREAM_AT` (BL-745), which BL-459 depends on; the HTTP server and DNS
  over QUIC never ask for it, so their bytes do not change.
- BL-458 to BL-460 build it, and the Curl port's client may connect to it as a live client in
  `Integration` tests (ADR-0083).
- The requirement matrix [`WebTransport.md`](../../Conformance/Extended/WebTransport.md) traces each
  server-side requirement to BL-458, BL-459 or BL-460; each task replaces its ID with the named test.
