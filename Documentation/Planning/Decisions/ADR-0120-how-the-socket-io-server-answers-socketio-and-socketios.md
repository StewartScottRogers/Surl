# ADR-0120 — How the Socket.IO server answers `socketio` and `socketios`

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05,
  in BL-452. BL-453 builds the library and its Engine.IO and Socket.IO packet codecs (decisions 0,
  3, 6 and the packet limits of 7), BL-454 serves sessions over the WebSocket transport (decisions
  1 to 7), BL-455 serves both schemes from `surl`, and BL-758 adds the HTTP long-polling transport
  and its upgrade to WebSocket (decision 2).
- **Builds on:** [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
  (the oracle is the specification; the Curl port is never proof; ports 80 and 443; one matrix per
  server), [ADR-0084](ADR-0084-the-libraries-the-extended-servers-share-for-http-1-1-websocket-and-dtls.md)
  decisions 1 and 2 (`Surl.HttpServing`'s `IHttpRequestHandler` and connection takeover,
  `Surl.WebSocket`'s upgrade, frames and close codes),
  [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) sections 4, 5,
  6, 8 and 10 (plain-text policy, refusals that name nothing, `IAuthenticationPolicy`, the refusal
  delay, `--self-signed`),
  [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  decision 4 (one account set; a bearer token is an empty-name account's password),
  [ADR-0071](ADR-0071-how-the-websocket-server-answers-upstream-curl.md) (the WebSocket upgrade
  checks and refusals), [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) (limits and fixed
  error text) and [ADR-0059](ADR-0059-how-a-protocol-server-tells-a-limit-from-shutdown.md)
  (shutdown grace).
- **Amends:** nothing.

## Context

`Surl.Extended.Protocol.SocketIo` (in `Protocols/Http/`, ADR-0083's table) answers the `socketio`
and `socketios` schemes the Curl port adds, on TCP port 80 and 443 when the listen URL names none
(ADR-0083 decision 7). By ADR-0083 decision 1 it is judged by its specifications, not by upstream
curl, which has no Socket.IO, and not by the Curl port. The specifications read, both on their
repositories' default branch `main`, which is where each version named lives (neither repository
has a `v4` or `v5` branch for the current version: `engine.io-protocol`'s `v2` and `v3` branches and
`socket.io-protocol`'s `v3` and `v4` branches hold the older revisions):

- **Engine.IO protocol, 4th version** (below "EIO"): github.com/socketio/engine.io-protocol `main`,
  commit `f21de7b00ed09b3bbad2807db718ea5d6bc36aba` (2024-07-01), `README.md`: "Transports" ("HTTP
  long-polling" with "Request path", "Query parameters", "Headers", "Sending and receiving data";
  "WebSocket"), "Protocol" (packet types 0 to 6), "Handshake", "Heartbeat", "Upgrade", "Message",
  "Packet encoding" and "History" ("From v3 to v4": the server sends the ping);
- **Socket.IO protocol, 5th version** (below "SIO"): github.com/socketio/socket.io-protocol `main`,
  commit `4f633dd6a4445492d57af3b27ddd2d336014f1f1` (2023-09-19), `Readme.md`: "Exchange protocol"
  (packet types 0 to 6), "Connection to a namespace", "Sending and receiving data",
  "Acknowledgement", "Disconnection from a namespace", "Packet encoding" ("Format" and "Examples")
  and "History" ("Difference between v5 and v4": the `CONNECT` payload and the `CONNECT_ERROR`
  object);
- **RFC 6455** for the WebSocket transport, through `Surl.WebSocket` (ADR-0084 decision 2).

The specifications give no numbers for `pingInterval`, `pingTimeout` or `maxPayload` beyond EIO
"Handshake"'s example (`25000`, `20000`, `1000000`); the values below take that example where
nothing in Surl already fixes one.

The port's `Curl.Extended.Protocol.SocketIo.UnitLibrary` (ADR-1035 there, with ADR-1210 and
ADR-1212) and its client matrix `SocketIo.md` were read only to learn what a client sends, so the
server accepts it: a WebSocket-only `GET /socket.io/?EIO=4&transport=websocket` (plus the listen
URL's own query), no `sid`, no subprotocol; after the open packet a CONNECT (`40` or `40/admin,`)
with no `auth` payload; one EVENT from `-d` once the CONNECT is answered; a `3` for every `2`; an
ACK `[]` for every server EVENT with an ack id; binary packets skipped. It never polls, never sends
binary, and never asks for an acknowledgement. Nothing below is taken from the port's output, and no
expected byte comes from it. Because the port sends no `auth` payload, BL-456 runs it against
`surl --allow-anonymous`.

## Decision

### 0. What the server is

- **One Engine.IO and Socket.IO server per listener.** `Surl.Extended.Protocol.SocketIo` references
  `Surl.Protocol.Abstractions`, `Surl.HttpServing` (BL-407) and `Surl.WebSocket` (BL-408), never
  another protocol server. Its `IConnectionProtocolServer` serves each connection through
  `Http1ConnectionServer` with the server's `IHttpRequestHandler`; a WebSocket upgrade returns an
  `HttpAnswer` with a connection takeover (ADR-0084 decision 1) that owns the connection from the
  `101` on. Time comes from `ExchangeContext.TimeProvider`.
- **HTTP/1.1 only.** WebSockets over HTTP/2 (RFC 8441) are not offered: `Surl.HttpServing` answers
  HTTP/1.1, and every Socket.IO client opens its WebSocket over HTTP/1.1.
- **`socketios`** is `socketio` inside TLS from the first byte, through the listener seam
  (ADR-0010); `--cert` or `--self-signed` is required, `CertificateProblem` (58) without, as for
  every TLS listener.
- **Not on ADR-0100's message bus.** ADR-0100 decision 3 names the bus's protocols and Socket.IO is
  not one; a broadcast (decision 5) reaches the sessions of its own listener only.
- **The default parser only.** SIO "Packet encoding" describes the default parser; the
  `socket.io-json-parser` and `socket.io-msgpack-parser` it mentions are custom parsers outside the
  protocol, and are not served.

### 1. The path and the protocol version (EIO "Request path", "Query parameters", "WebSocket"; SIO "History")

- **The path is `/socket.io/`**, the one SIO's Engine.IO uses (EIO "Request path": "the Socket.IO
  protocol uses `/socket.io/`"), whatever the listen URL's path (which names a namespace, decision
  4). It is compared byte for byte without the query; `/socket.io` without the slash and every other
  path is `404 Not Found` with no body.
- **The query** is read for `EIO`, `transport` and `sid`; other parameters (a client's own, such as
  the port's appended listen-URL query or the JavaScript client's `t` cache-buster) are ignored.
- **`EIO=4` is required.** EIO "Query parameters" makes `EIO` mandatory and a missing mandatory
  parameter `400`; SIO "History" pins version 5 to `EIO=4`. An `EIO` that is absent, or is anything
  but `4` (an Engine.IO v3 client's `EIO=3` among them), is answered before any upgrade with the
  refusal below, code 5.
- **Refusals** are `400 Bad Request`, `Content-Type: application/json`, and one fixed body naming
  an error code. EIO requires only the status; the body adds the code and text the reference
  implementation uses (`engine.io`'s error table), so a JavaScript client's `connect_error` names the
  cause, and every text is fixed ASCII (ADR-0006):

  | Code | Body | When |
  | --- | --- | --- |
  | 0 | `{"code":0,"message":"Transport unknown"}` | `transport` absent, or neither `websocket` nor `polling` (and `polling` until BL-758) |
  | 1 | `{"code":1,"message":"Session ID unknown"}` | a `sid` no live session has |
  | 2 | `{"code":2,"message":"Bad handshake method"}` | a request without `sid` whose method is not `GET` |
  | 3 | `{"code":3,"message":"Bad request"}` | a second WebSocket for a session that has one, a second concurrent poll or post (BL-758), or a WebSocket request that is not an upgrade |
  | 5 | `{"code":5,"message":"Unsupported protocol version"}` | `EIO` absent or not `4` |

  A path other than `/socket.io/` stays `404` (not a code), and the WebSocket upgrade's own checks
  answer first, with `Surl.WebSocket`'s refusals (ADR-0071), once the query is good.

### 2. The transports (EIO "Transports", "Handshake", "Upgrade", "Packet encoding")

- **WebSocket, connected directly** (EIO "Handshake", "WebSocket-only session"): a `GET
  /socket.io/?EIO=4&transport=websocket` without `sid` is a WebSocket upgrade checked by
  `Surl.WebSocket` (ADR-0071's order and refusals), no subprotocol chosen; the `101` opens a new
  session and the open packet (decision 3) is its first message. One Engine.IO packet is one
  WebSocket message (EIO "WebSocket"): a text message for a packet with a text payload, a binary
  message, with no type prefix, for a binary `message` packet. The server writes each packet as one
  unfragmented message and reads fragmented ones.
- **HTTP long-polling and the upgrade are served,** by BL-758, because a complete Engine.IO server
  serves both transports and polling is every reference client's default first transport (EIO
  "Upgrade": "By default, the client SHOULD create an HTTP long-polling connection"). What BL-758
  builds, from EIO "HTTP long-polling" and "Upgrade":
  - `GET /socket.io/?EIO=4&transport=polling` without `sid` opens a session: `200`,
    `Content-Type: text/plain; charset=UTF-8`, the open packet as the body, with `upgrades`
    `["websocket"]`;
  - a `GET` with `sid` is held until a packet is queued for the session (or the heartbeat's next
    `2`), then answered `200` with every queued packet joined by `\x1e`, binary payloads `b` plus
    base64 (EIO "Packet encoding", "HTTP long-polling"), `Content-Type: text/plain; charset=UTF-8`;
  - a `POST` with `sid` is read within `--max-message` (decision 7), split at `\x1e`, each packet
    handled, and answered `200` `Content-Type: text/html`, body `ok` (EIO "Sending data"); a body
    with `Content-Type: application/octet-stream` is one binary packet (EIO "Headers");
  - an unknown `sid` is code 1; a second concurrent `GET`, or a second concurrent `POST`, is code
    3 and closes the session (EIO "Sending data", "Receiving data": "the server MUST return an HTTP
    400 error status and close the session");
  - the upgrade: a WebSocket request carrying the session's `sid` is upgraded, its `2probe`
    answered `3probe`, any held `GET` answered `6` (noop), and the client's `5` moves the session to
    the WebSocket, after which a polling request with that `sid` is code 3.
  Until BL-758 lands, `transport=polling` is code 0 and the open packet's `upgrades` is `[]` on
  every session, so no client is offered an upgrade the server cannot finish.
- **On a WebSocket session `upgrades` is `[]`**: there is no better transport to move to.
- **The Engine.IO packets the server reads** (EIO "Protocol"): `1` close ends the session (the
  server closes the WebSocket `1000`, sends nothing more); `2` ping is answered `3probe` only as
  `2probe` during BL-758's upgrade and is otherwise ignored, since in v4 the server pings (EIO
  "History"); `3` pong is the heartbeat's answer (decision 3); `4` message carries a Socket.IO packet
  (decisions 4 to 6); `5` upgrade is read only during BL-758's upgrade; `6` noop is ignored. A
  client `0` open, a `5` outside an upgrade, an empty text message, or a type character outside `0`
  to `6`, is a **protocol error**: the WebSocket is closed `1002` with no Engine.IO packet first
  (EIO names no error packet).

### 3. The open packet and the heartbeat (EIO "Handshake", "Heartbeat")

- **The open packet** is `0` and this JSON object, in this member order, with no whitespace:

  ```
  0{"sid":"<sid>","upgrades":[],"pingInterval":25000,"pingTimeout":20000,"maxPayload":<n>}
  ```

  `sid` is 15 bytes from the BCL's `RandomNumberGenerator`, base64url without padding (20
  characters, the length of EIO's example `lv_VI97HAXpY6yYWAAAC`). `pingInterval` and `pingTimeout`
  are EIO's example values. `maxPayload` is `--max-message` (1048576 by default; decision 7), so the
  packet limit a client is told is the one the server enforces.
- **The heartbeat**, on the injected `TimeProvider`: `pingInterval` after the open packet, and
  `pingInterval` after each pong, the server sends `2`. When no `3` arrives within `pingTimeout` of
  that `2`, the session is closed (EIO: the server "SHOULD consider that the connection is closed"):
  the WebSocket is closed `1000` with no Engine.IO packet first, since the peer is presumed gone.
  Only a pong restarts the heartbeat; other packets do not (EIO names the pong as the answer). A
  pong with no ping outstanding is ignored.
- Both intervals are fixed and lie inside `--idle-timeout`'s 120 s (25 s + 20 s = 45 s), so the
  heartbeat ends a silent client before `Surl.Core` discards it, and a live client's pongs keep the
  connection from idling out. No option sets them: no test harness needs another value that a slow
  test cannot meet with these.

### 4. Namespaces and their authentication (SIO "Connection to a namespace", "History"; ADR-0032; ADR-0100 decision 4)

- **Namespaces served:** the main namespace `/`, and the listen URL's path when it is not empty or
  `/` (`socketio://0.0.0.0:3000/admin` serves `/` and `/admin`), the path with any trailing `/`
  removed, compared byte for byte. That mirrors how a client names a namespace by its URL's path
  (`io("http://host/admin")`) and gives a test both the main and a named namespace.
- **CONNECT first** (SIO: "If the server does not receive a CONNECT packet first, then it MUST close
  the connection immediately"): a session's first Socket.IO packet must be a CONNECT, or the session
  is closed as a protocol error (decision 2's `1002`). After one namespace is connected, the client
  may CONNECT more (SIO: "MAY be connected to multiple namespaces"); a CONNECT for a namespace
  already connected on the session is ignored, the existing connection kept.
- **The connect timeout:** a session with no namespace connected for `--head-timeout` (30 s) — from
  its open packet, or from its last namespace's DISCONNECT — is closed, its WebSocket `1000`, the
  slow-sender bound every server uses.
- **The CONNECT payload** is absent or a JSON object (SIO "History": "the `CONNECT` packet now can
  contain a payload"); any other JSON is a protocol error. The object's members are read for
  credentials, in this order:
  - `token`, a string: a bearer token, the password of the empty-name account (ADR-0100 decision
    4);
  - otherwise `username` and `password`, both strings: that account's login;
  - otherwise no credentials (other members are ignored, never echoed).
  The login is judged by `IAuthenticationPolicy.CheckPasswordLoginAsync` as a `PasswordLogin` with
  the scheme, the name (empty for a token, `null` for none), the password bytes in UTF-8 and the
  connection's `TlsSession`. `Accepted` and `AcceptedUnchecked` connect the namespace;
  `RefusedCredentials`, `RefusedAnonymous` and `RefusedPlaintext` refuse it.
- **Secure by default:** with no `--allow-anonymous`, a CONNECT without credentials is
  `RefusedAnonymous`; a password over plain `socketio` is `RefusedPlaintext` unless
  `--allow-plaintext-auth` is given (ADR-0032 section 4), and `socketios` logs in by default. Under
  `--allow-anonymous` a CONNECT without credentials is served, and credentials given are still
  handed to the policy, whose verdict stands, as for every server.
- **The HTTP requests carry no login.** EIO and SIO put authentication in the CONNECT payload, and a
  browser's WebSocket cannot set `Authorization`; an `Authorization` field on the upgrade or a poll
  is ignored. An Engine.IO session without a connected namespace can do nothing but answer pings,
  and the connect timeout ends it.
- **The CONNECT answer** (SIO "Connection to a namespace", "Packet encoding"): success is CONNECT
  with `{"sid":"<id>"}`, `40{"sid":"…"}` for `/` and `40/admin,{"sid":"…"}` for another namespace,
  the id 15 random bytes in base64url (20 characters), fresh for each namespace connection and
  independent of the Engine.IO `sid` (SIO "History": the Socket.IO id differs from the Engine.IO
  one). Refusal is CONNECT_ERROR with a `message` (SIO "History": "the payload `CONNECT_ERROR`
  packet is now an object"), one fixed text each:

  | Case | Packet (main namespace shown) |
  | --- | --- |
  | a namespace not served | `44{"message":"Invalid namespace"}` |
  | any refused login, after the policy's refusal delay (ADR-0032 section 8) | `44{"message":"Not authorized"}` |

  The texts name neither the account nor the cause (ADR-0032 section 5); `Not authorized` is SIO's
  own example. A refusal leaves the session open, so the client may CONNECT again; the connect
  timeout still runs.

### 5. Events and acknowledgements (SIO "Sending and receiving data", "Acknowledgement", "Disconnection from a namespace")

- **An EVENT's payload** must be a non-empty JSON array whose first element is a string, the event
  name (SIO: "MUST be a non-empty array. If that's not the case, then the receiver MUST close the
  connection"); anything else is a protocol error. An EVENT for a namespace the session has not
  connected is dropped, unanswered.
- **Echo:** an EVENT without an ack id is sent back to its sender on the same namespace, as an
  EVENT with the same name and arguments, the JSON array byte for byte as received, with no ack id
  (`42["hello",1]` answered `42["hello",1]`).
- **Broadcast:** an EVENT named `broadcast` is instead sent, the same way, to every session
  connected to that namespace on this listener, its sender included, so the sender sees it once.
  Other names are echoed only.
- **Acknowledgement** (SIO: "The receiver MUST respond with an `ACK` packet with the same event
  ID"): an EVENT with an ack id is answered by one ACK carrying that id and the event's arguments
  (the array without its first element, possibly empty), byte for byte, on the same namespace
  (`421["hello",1]` answered `431[1]`) — and is not echoed, the ACK being its answer. A `broadcast`
  with an ack id is broadcast, then ACKed. Ack ids are non-negative decimal integers of at most 15
  digits (decision 7).
- **The server sends no ack id of its own**, so a client ACK or BINARY_ACK has nothing to answer and
  is read and dropped (after its attachments, decision 6).
- **DISCONNECT** from the client (`41`, `41/admin,`) leaves that namespace; nothing is answered (SIO:
  "No response is expected"). The Engine.IO session stays (SIO: "MAY be kept alive") while another
  namespace is connected, and the connect timeout of decision 4 runs once none is. A DISCONNECT for
  a namespace not connected is ignored.
- **The Socket.IO packets the server sends**, exactly: CONNECT with `sid`, CONNECT_ERROR, EVENT and
  BINARY_EVENT (echo and broadcast), ACK and BINARY_ACK, and DISCONNECT at shutdown (decision 7).
  A client CONNECT_ERROR, or a packet type outside 0 to 6, is a protocol error.

### 6. Binary attachments (SIO "Packet encoding", "Sending and receiving data", "Acknowledgement")

- **Reading.** A BINARY_EVENT or BINARY_ACK is `5` or `6`, the attachment count, `-`, then the
  namespace and ack id as for other packets, then the JSON payload in which each binary value is a
  placeholder `{"_placeholder":true,"num":N}` (SIO "Format"; `451-["baz",{"_placeholder":true,"num":0}]`
  on the wire). The next N Engine.IO packets on the session must be binary `message` packets, the
  attachments in order; they belong to the packet, not packets of their own. A text packet before
  they have all arrived is a protocol error. The count must be at least 1; every placeholder's
  `num` must be an integer from 0 to N − 1, each number used exactly once, and an object with
  `_placeholder` must have exactly those two members; anything else is a protocol error.
- **Answering.** Placeholders are kept as received, not decoded: echo and broadcast send a
  BINARY_EVENT whose text part is the received one with any ack id removed, then the same attachments
  in order, byte for byte; an ack id is answered by a BINARY_ACK with the same count, the same id and
  the arguments (the payload without its name, which is a string and so never a placeholder), then
  the same attachments. A plain EVENT whose payload holds a placeholder object is read as JSON like
  any other, since only a binary packet has attachments.
- **Over BL-758's polling**, each attachment travels as a `b`-prefixed base64 packet (EIO "Packet
  encoding"), the same bytes once decoded.

### 7. Limits (ADR-0006) and shutdown

| Limit | How the server applies it | Answer when hit |
| --- | --- | --- |
| connections (`--max-connections`, `--max-connections-per-address`) | `Surl.Core`'s refusal before HTTP | the connection refused |
| request head, slow sender (`--max-request-head`, `--head-timeout`) | `Http1ConnectionServer` (ADR-0084 decision 1) | its answer (`431`, `408` or close) |
| sessions on a listener | 1024 (ADR-0006's connection ceiling) | a new session's upgrade or handshake poll: `503 Service Unavailable`, no body |
| an Engine.IO packet: one WebSocket message, or one packet of a polling body | `maxPayload`, which is `--max-message` (1048576) | WebSocket Close `1009`; a polling `POST` body past it is `413` and the session closed (BL-758) |
| a WebSocket frame (`--max-message`) | `Surl.WebSocket` | WebSocket Close `1009` |
| attachments in one binary packet | at most 64, and the text part plus every attachment within `--max-message` together | protocol error, WebSocket Close `1009` for the size, `1002` for the count |
| JSON nesting | 64 levels (`Utf8JsonReader`'s default) | protocol error, WebSocket Close `1002` |
| a namespace name | 128 bytes | protocol error, WebSocket Close `1002` |
| an ack id | at most 15 decimal digits | protocol error, WebSocket Close `1002` |
| no namespace connected (`--head-timeout`, 30 s) | decision 4 | WebSocket Close `1000` |
| heartbeat | 25 s interval, 20 s timeout (decision 3) | WebSocket Close `1000` |
| unsent packets a session | 1048576 bytes; a broadcast that would pass it is not queued for that session, which is then closed | WebSocket Close `1008` |
| `--idle-timeout` (120 s), `--max-time` (3600 s) | `Surl.Core` (ADR-0006) | the connection discarded |
| shutdown | each connected namespace gets a DISCONNECT (`41`, `41/admin,`), then the WebSocket is closed `1001`, within the grace (ADR-0059) | as said |

No new option is added: ADR-0006's options cover what a test harness tunes.

## Alternatives considered

- **WebSocket only, for good**, as the port's client is. Rejected: a complete Engine.IO server
  serves both transports and every reference client starts on polling; WebSocket lands first
  (BL-454) because it is the full-duplex one and the one the port uses, and BL-758 adds polling.
- **Authenticate the HTTP requests** through `IHttpAuthenticationSession`, as ADR-0119 does for
  SignalR. Rejected: SIO carries credentials in the CONNECT payload, a browser's WebSocket cannot set
  `Authorization`, and judging both would demand two logins of one client; a session that never
  connects a namespace can do nothing and is closed by the connect timeout.
- **Serve every namespace a client names.** Rejected: SIO makes CONNECT_ERROR the answer to a
  namespace that is not allowed, and a fixed set (`/` and the listen URL's path) lets a test see
  both answers.
- **Echo an acknowledged event as well as ACK it.** Rejected: the ACK is the answer the sender asked
  for, and a second copy would make every acknowledged emit look like two events to the client.
- **Empty-bodied `400` refusals.** Rejected: EIO fixes only the status, and the reference
  implementation's coded JSON bodies tell a client why at no cost; every text is fixed.
- **A `maxPayload` of EIO's example `1000000`** with its own bound. Rejected: tying it to
  `--max-message` gives a test harness one option for both the WebSocket frame and the packet limit.
- **Options for the heartbeat.** Rejected for now: no test needs other values; a later ADR may add
  one.

## Consequences

- `surl socketio://127.0.0.1:3000/admin` (with an account, or `--allow-anonymous`) is a Socket.IO
  server on `/socket.io/` that opens Engine.IO v4 sessions over WebSocket, pings every 25 s, connects
  `/` and `/admin` after checking the CONNECT payload, echoes events, broadcasts `broadcast`,
  acknowledges events that ask, and carries binary attachments; `socketios://` is the same inside
  TLS. BL-758 adds HTTP long-polling and its upgrade.
- BL-453 to BL-455 build it, BL-758 completes it, and the Curl port's client may connect to it as a
  live client in `Integration` tests (BL-456, ADR-0083).
- The requirement matrix [`SocketIo.md`](../../Conformance/Extended/SocketIo.md) traces each
  server-side requirement to BL-453, BL-454 or a later task; each task replaces its ID with the
  named test.
