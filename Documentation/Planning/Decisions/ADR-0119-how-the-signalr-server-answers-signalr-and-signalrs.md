# ADR-0119 — How the SignalR server answers `signalr` and `signalrs`

- **Status:** Accepted
- **Date:** 2026-11-05
- **Decided by:** Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), 2026-11-05,
  in BL-447. BL-448 builds the library, negotiate, the bearer login and the JSON hub-protocol codec
  (decisions 0, 1, 2, 3, 5 and the record limits of 7), BL-449 serves hub invocations over the
  WebSocket transport with keep-alive and timeouts (decisions 1, 3, 4, 6 and 7), BL-450 serves both
  schemes from `surl`, BL-757 adds the Server-Sent Events and Long Polling transports (decision 2),
  BL-755 stateful reconnect (decision 6) and BL-756 the MessagePack hub protocol (decision 3).
- **Builds on:** [ADR-0083](ADR-0083-how-surl-serves-the-curl-ports-extended-protocols-and-how-they-are-tested.md)
  (the oracle is the specification; the Curl port is never proof; ports 80 and 443; one matrix per
  server), [ADR-0084](ADR-0084-the-libraries-the-extended-servers-share-for-http-1-1-websocket-and-dtls.md)
  decisions 1, 2 and 4 (`Surl.HttpServing`'s `IHttpRequestHandler` and connection takeover,
  `Surl.WebSocket`'s upgrade, frames and close codes, logins through `IHttpAuthenticationSession`),
  [ADR-0032](ADR-0032-secure-by-default-authentication-accounts-and-self-signed.md) sections 4, 6, 8
  and 10 (HTTP policy, the session contract, the refusal delay, `--self-signed`),
  [ADR-0100](ADR-0100-the-stores-synergetic-protocols-share-files-mail-the-message-bus-and-accounts.md)
  decision 4 (one account set; a Bearer token is an empty-name account's password),
  [ADR-0071](ADR-0071-how-the-websocket-server-answers-upstream-curl.md) (the WebSocket upgrade
  checks and refusals), [ADR-0006](ADR-0006-hardening-for-internet-facing-use.md) (limits and fixed
  error text) and [ADR-0059](ADR-0059-how-a-protocol-server-tells-a-limit-from-shutdown.md)
  (shutdown grace).
- **Amends:** nothing.

## Context

`Surl.Extended.Protocol.SignalR` (in `Protocols/Http/`, ADR-0083's table) answers the `signalr` and
`signalrs` schemes the Curl port adds, on TCP port 80 and 443 when the listen URL names none
(ADR-0083 decision 7). By ADR-0083 decision 1 it is judged by its specification, not by upstream
curl, which has no SignalR, and not by the Curl port. ASP.NET Core SignalR's specifications carry
no tag; the ones read are at dotnet/aspnetcore `main`, commit
`6aacb16a27a677d16278a9814ed04f871020854b` (2026-11-05), whose `src/SignalR/docs/specs` was last
changed by commit `fa8126f62f64eaf37292ff1e334ace99bc757bcf` (2026-07-02):

- **`TransportProtocols.md`** (below "TP"): "Transport Requirements", "`POST [endpoint-base]/negotiate`
  request" with its "Version 1", "Version 0" and "All versions" sections (the redirect and `error`
  forms), "Transfer Formats", "WebSockets (Full Duplex)", "HTTP Post", "Server-Sent Events" and
  "Long Polling";
- **`HubProtocol.md`** (below "HUB"): "Overview" (the handshake, `0x1E`), "Invocations", "Message
  Headers", "Non-Blocking Invocations", "Streaming", "Cancellation", "Upload streaming", "Completion
  and results", "Errors", "Ping (aka Keep Alive)", "JSON Encoding" (message types 1 to 9) and
  "MessagePack (MsgPack) encoding";
- **RFC 6455** for the WebSocket transport, through `Surl.WebSocket` (ADR-0084 decision 2).

The specifications do not state the keep-alive interval or the client timeout as numbers; HUB "Ping"
says "regular intervals". The 15 s and 30 s below are ASP.NET Core's documented defaults
(`KeepAliveInterval`, `ClientTimeoutInterval`), taken as the planning assumption the task gives.

The port's `Curl.Extended.Protocol.SignalR.UnitLibrary` (ADR-1084 there) and its client matrix
`SignalR.md` were read only to learn what a client sends, so the server accepts it: `POST
<path>/negotiate?negotiateVersion=1` with `Content-Length: 0` and no `Authorization` (a bearer token
only after a negotiate redirect), one redirect followed, then an HTTP/1.1 WebSocket upgrade to
`<path>?id=<connectionToken>` (or `connectionId` for version 0), the handshake
`{"protocol":"json","version":1}`, one Invocation (type 1) or StreamInvocation (type 4) built from
`-d`, masked text frames, and a WebSocket Close at the end. It never cancels, never streams to the
server and never reconnects. Nothing below is taken from the port's output, and no expected byte
comes from it. Because the port sends no credentials of its own on the first negotiate, BL-451
runs it against `surl --allow-anonymous`.

## Decision

### 0. What the server is

- **One SignalR server, one hub per listener.** `Surl.Extended.Protocol.SignalR` references
  `Surl.Protocol.Abstractions`, `Surl.HttpServing` (BL-407) and `Surl.WebSocket` (BL-408), never
  another protocol server. Its `IConnectionProtocolServer` serves each connection through
  `Http1ConnectionServer` with the hub's `IHttpRequestHandler`; a WebSocket upgrade returns an
  `HttpAnswer` with a connection takeover (ADR-0084 decision 1) that owns the connection from the
  `101` on. Time comes from `ExchangeContext.TimeProvider`.
- **HTTP/1.1 only.** WebSockets over HTTP/2 (RFC 8441) are not offered: `Surl.HttpServing` answers
  HTTP/1.1, and a SignalR client falls back to HTTP/1.1 for its upgrade.
- **`signalrs`** is `signalr` inside TLS from the first byte, through the listener seam (ADR-0010);
  `--cert` or `--self-signed` is required, `CertificateProblem` (58) without, as for every TLS
  listener.
- **The hub is not on ADR-0100's message bus.** ADR-0100 decision 3 names the bus's protocols and
  SignalR is not one; its broadcast (decision 1) reaches the connections of its own hub only.

### 1. The hub path and the hub's methods (TP "negotiate", "WebSockets"; HUB "Invocations", "Streaming", "Upload streaming", "Completion and results")

- **`[endpoint-base]`** is the listen URL's path with any trailing `/` removed:
  `signalr://0.0.0.0:5000/chat` serves the hub at `/chat`, and a listen URL with no path (or `/`)
  serves it at `/`. Negotiate is `[endpoint-base]/negotiate` (`/negotiate` for the root hub). Paths
  are compared byte for byte, without their query. Every other path is `404 Not Found`, judged after
  authentication (decision 5) so an unauthenticated client learns nothing about the paths served.
- **Methods**, by `target` (case-sensitive, HUB "Invocations"); each argument is any JSON token:

  | Target | Called as | Arguments | Answer |
  | --- | --- | --- | --- |
  | `Echo` | Invocation | exactly 1 | Completion whose `result` is the argument, byte for byte as received |
  | `Broadcast` | Invocation | any number | every connection on the hub that has finished its handshake, the caller included, receives a non-blocking Invocation `{"type":1,"target":"Broadcast","arguments":[...]}` with the same arguments, byte for byte; then the caller's Completion with no `result` (a void method) |
  | `Count` | StreamInvocation | exactly 1, an integer from 0 to 1000 | StreamItems `1`, `2`, ... `n`, the first at once and each next 100 ms later on the `TimeProvider`, then a Completion with no `result` |
  | `Collect` | Invocation or StreamInvocation | none, and exactly one `streamIds` entry | reads the client's StreamItems for that stream ID until the client's Completion of it, then completes with `result` an array of the items in order (Invocation), or sends each item back as a StreamItem as it arrives and completes after the client's Completion (StreamInvocation) |
  | `Fail` | Invocation or StreamInvocation | any | Completion with `error` `An unexpected error occurred invoking 'Fail' on the server.` |

  `Echo`, `Broadcast`, `Count` and `Fail` make message types 1, 2 and 3 reachable from the server,
  `Count` makes CancelInvocation (5) meaningful, and `Collect` covers HUB "Upload streaming". The
  error texts are fixed ASCII (ADR-0006, TP's note: "the detailed exception message is never
  provided"); the method name in them is the server's own, never the client's text.
- **A call the hub cannot serve** is answered with a Completion carrying `error`, one fixed text
  each (HUB "Completion and results" requires a Completion with an error for a streaming method
  invoked as an Invocation and the reverse):

  | Case | `error` |
  | --- | --- |
  | an unknown `target` | `Unknown hub method.` |
  | `Count` as an Invocation, or `Echo` or `Broadcast` as a StreamInvocation | `The method was invoked with the wrong message type.` |
  | wrong argument count or kind, or `streamIds` where the method takes none, or none where it takes one | `Invalid arguments for the hub method.` |
  | `Collect` whose items reach `--max-filesize` | `The upload stream is too large.` |
  | more than 100 invocations in progress on the connection (streams and uploads count until their Completion) | `Too many invocations in progress.` |

- **A non-blocking Invocation** (no `invocationId`, HUB "Non-Blocking Invocations") runs the method
  and sends nothing for it, not even an error. `Broadcast` without an `invocationId` still
  broadcasts. A StreamInvocation without `invocationId` is a protocol error (decision 3).
- **Cancellation** (HUB "Streaming", "Cancellation"): a CancelInvocation naming a `Count` or a
  streaming `Collect` in progress stops its StreamItems and sends its Completion with no `result`
  at once. One naming a non-streaming Invocation in progress (`Collect` waiting for its upload) ends
  it with a Completion with no `result` too, as the "Cancellation" section's MUST requires. One
  naming no invocation in progress is ignored (its SHOULD).
- **Message headers** (`headers`, HUB "Message Headers") are read and ignored; the server sends none.

### 2. The transports and negotiate (TP "negotiate", "Transfer Formats", "WebSockets", "HTTP Post", "Server-Sent Events", "Long Polling")

- **Negotiate** is `POST [endpoint-base]/negotiate`; another method on that path is `405` with
  `Allow: POST`. Any request body is read and discarded within `--max-filesize`. The query's
  `negotiateVersion` (TP "negotiateVersion") picks the response: absent or `0` gives version 0, `1`
  or higher gives version 1 (the server's largest); a value that is not a non-negative integer is
  `400 Bad Request`. The server supports version 0, so no request is refused for its version.
- **The success response** is `200`, `Content-Type: application/json`, and one JSON object, in this
  member order, with no whitespace:

  ```
  {"negotiateVersion":1,"connectionId":"<id>","connectionToken":"<token>","availableTransports":[{"transport":"WebSockets","transferFormats":["Text","Binary"]}]}
  ```

  Version 0 omits `connectionToken`. `connectionId` and `connectionToken` are each 16 bytes from the
  BCL's `RandomNumberGenerator`, base64url without padding (22 characters), independent of each other
  (TP "Version 1": the token "should be kept secret", so it is not derivable from the id).
- **`availableTransports` lists exactly what is served.** Until BL-757 it is WebSockets alone, with
  both transfer formats (RFC 6455 carries text and binary frames; decision 3 accepts JSON in either).
  BL-757 adds `{"transport":"ServerSentEvents","transferFormats":["Text"]}` and
  `{"transport":"LongPolling","transferFormats":["Text","Binary"]}`, with HTTP Post for the client's
  half, each as TP's section for it says (the mandatory `id`, `400` without it, `404` for an unknown
  one, `409` for a second poll or post, `204` to end a poll, `DELETE` to end the connection), and
  changes this list then. Every transport is served because a complete SignalR server offers all
  three; WebSockets lands first because it is the only full-duplex one and the one the Curl port
  uses.
- **A transport not offered:** until BL-757, a `GET [endpoint-base]` that is not a WebSocket upgrade
  (Server-Sent Events, a Long Polling poll) is answered by `Surl.WebSocket`'s refusal for a missing
  upgrade (ADR-0071: `426 Upgrade Required` with `Sec-WebSocket-Version: 13`), and `POST` or
  `DELETE` on `[endpoint-base]` is `405` with `Allow: GET`.
- **The negotiate `error` form** (TP "All versions") is sent, `200` with
  `{"error":"<text>"}`, only when the hub is full (decision 7): `{"error":"The server has too many
  connections."}`. Everything else refused at negotiate is an HTTP status, because a client reads a
  status without parsing a body.
- **The redirect form** (`url`, `accessToken`) is never sent: the hub is the endpoint it would
  redirect to.
- **The WebSocket transport** (TP "WebSockets"): a `GET [endpoint-base]` upgrade, checked by
  `Surl.WebSocket` (ADR-0071's order and refusals), with no subprotocol chosen. Then the `id`:
  - absent: a new connection, as if negotiated now (TP: "the client is not required to use the
    negotiate request");
  - a `connectionToken` from a version 1 negotiate, or a `connectionId` from a version 0 one, not
    yet used: that connection;
  - a token whose connection already has its WebSocket: `409 Conflict`;
  - anything else, including a version 1 connection's `connectionId`, an expired token and one whose
    connection has ended: `404 Not Found`.
  A negotiated connection not upgraded within `--head-timeout` of its negotiate response is
  forgotten, so its token is then `404`. Refusals have no body.

### 3. The handshake and the JSON hub protocol (HUB "Overview", "JSON Encoding", "Errors")

- **Records.** Every message is one JSON object ended by `0x1E`. Records are cut at `0x1E` across
  the WebSocket messages that carry them: a message may hold several records, a record may span
  messages, and the handshake request may be followed by other records in the same message (HUB
  "Overview"). Text and binary WebSocket messages are both read. The server writes each record as
  one text message, several records never combined.
- **The handshake request** must be the first record, read within `--head-timeout` of the `101`. It
  is accepted when it is a JSON object whose `protocol` is the string `json` and whose `version` is
  the number `1`; other members are ignored. The answer is `{}` then `0x1E`.
- **A handshake the server refuses** is answered with one handshake response, `{"error":"<text>"}`
  then `0x1E`, then a WebSocket Close `1000` with no reason (HUB "Overview": "the server must close
  the connection"), no other message sent first:

  | Case | `error` |
  | --- | --- |
  | `protocol` is a string other than `json`, or `version` is a number other than `1` | `Requested protocol is not available.` |
  | the record is not JSON, not an object, or lacks `protocol` or `version` | `The handshake request is invalid.` |

  The text never repeats the client's protocol name (ADR-0006). Until BL-756, `messagepack` is
  refused this way; BL-756 accepts `{"protocol":"messagepack","version":1}`, then reads and writes
  HUB "MessagePack (MsgPack) encoding" in binary messages, so a complete SignalR server speaks both
  of the specification's encodings.
- **A handshake request not received within `--head-timeout`** ends the connection with a WebSocket
  Close `1008` (policy violation) and no handshake response.
- **Message fields** follow HUB "JSON Encoding": `type` is required; `invocationId`, `target` and
  `arguments` as each type needs; `streamIds`, `headers`, `item`, `result`, `error` and
  `allowReconnect` as each type allows. Members the server does not know are ignored: HUB "Errors"
  lets an endpoint terminate on an unrecognized field ("may"), and ignoring them keeps clients that
  add members working.
- **What the server sends**, exactly: the handshake response; Completion (3) with `result`, with
  `error`, or with neither; StreamItem (2); the Broadcast Invocation (1) without `invocationId`; Ping
  (6) `{"type":6}`; Close (7) `{"type":7}` or `{"type":7,"error":"<text>"}`. It never sends
  StreamInvocation or CancelInvocation, never sends `allowReconnect: true`, and never invokes a
  client method that expects an answer.
- **Protocol errors** (HUB "Errors") end the connection: a Close
  `{"type":7,"error":"Connection closed with an error."}`, then a WebSocket Close `1002` (TP: "a
  non-1000 Normal Closure status code" for errors). They are: a record that is not a JSON object; a
  missing or non-number `type`, or a type the server does not read (anything but 1 to 7, and 8 and 9
  until BL-755); a required member missing or of the wrong JSON kind; an `invocationId` already in
  progress on this connection; a client StreamItem or Completion naming no upload stream the server
  is reading; a client StreamItem after that stream's Completion; a client Completion with both
  `result` and `error`; a StreamInvocation without `invocationId`; a handshake request after the
  handshake.
- **A client Close** (type 7, with or without `error`) ends the connection: the server stops every
  invocation, sends nothing more, and closes the WebSocket `1000`.

### 4. Keep-alive and the client timeout (HUB "Ping (aka Keep Alive)")

- **Keep-alive:** after the handshake, the server sends a Ping `{"type":6}` whenever it has sent no
  record for 15 s ("A `Ping` is only sent if the interval has elapsed without a message being
  sent"). Any record sent restarts the 15 s.
- **Client timeout:** a connection from which the server has received no record for 30 s after its
  handshake is closed: a Close
  `{"type":7,"error":"Server timeout elapsed without receiving a message from the client."}`, then a
  WebSocket Close `1000`. Any record restarts the 30 s; a WebSocket Ping or Pong frame does not, since
  the hub protocol's own Ping is the liveness signal.
- **A client's Ping** needs no answer (HUB: "no obligation to respond") and gets none; it only
  restarts the timeout.
- Both intervals are fixed, on the injected `TimeProvider`, and both lie inside `--idle-timeout`'s
  120 s, so the hub's own timeout ends a silent client before `Surl.Core` discards it. No option sets
  them: no test harness needs another value that a slow test cannot meet with the defaults.

### 5. Authentication (ADR-0032; ADR-0100 decision 4)

- **Secure by default.** Negotiate, the WebSocket upgrade and every other request are judged through
  the connection's `IHttpAuthenticationSession` (`StartHttpConnection`, ADR-0032 section 6) with
  `HttpAuthenticationRequest(<method>, <path>, IsWrite: false, <fields>)`, reading the one account
  set of `--user`, `--user-file` and the layer-2 options (ADR-0100 decision 4), never a private
  credential source. The session's HTTP policy decides the schemes and whether a password may cross
  plain `signalr` (ADR-0032 section 4, `--allow-plaintext-auth`), as for the HTTP server.
- **The bearer token** (the task's planning name) is `Authorization: Bearer <token>`, an empty-name
  account whose password is the token (ADR-0100 decision 4). Because a browser's WebSocket cannot
  set a header, an `access_token` query parameter on the upgrade or on negotiate is read as
  `Authorization: Bearer <value>` when the request has no `Authorization` field; with one, the query
  parameter is ignored. `Basic` and the session's request-bound schemes (Digest) are accepted the
  same way; NTLM and Negotiate are accepted as the session offers them, the connection being HTTP/1.1.
- **Verdicts:** `Proceed` continues. `Challenge` is `401` with the verdict's `WWW-Authenticate`
  values and no body; `Forbidden` is `403` with no body. Neither names the account or the cause, and
  each waits the session's refusal delay (ADR-0032 section 8). A negotiate without a token is
  therefore `401`, not the negotiate `error` form.
- **The upgrade must be the negotiated caller.** A connection negotiated by an account is upgraded
  only by the same account (or by anonymous when it was negotiated anonymously); another account is
  `403`. A connection is never served to a caller other than the one that negotiated it.
- **`--allow-anonymous`** serves a request without credentials as the anonymous caller; one that
  carries credentials is still judged, and a bad one is never served as anonymous.
- Authorization is all or nothing: the hub touches no store, so every method is open to a caller the
  session admits.

### 6. Stateful reconnect (HUB `Ack` and `Sequence`; TP "useAck")

Stateful reconnect is served, by BL-755; until then the negotiate response never offers it and
types 8 and 9 are protocol errors (decision 3). What BL-755 builds:

- **Offered when asked.** A negotiate whose query has `useStatefulReconnect=true` (the name ASP.NET
  Core's clients send) or `useAck=true` (the name TP gives) gets `"useStatefulReconnect":true` in
  its response, after `availableTransports`. Without it, a lost WebSocket ends the connection, and a
  reused token is `404` (TP: "the client must not reuse connectionToken").
- **Sequencing.** On such a connection the server numbers each Invocation, StreamItem and Completion
  it sends from 1, keeps each until an Ack covers it, and acknowledges the client's: after receiving
  any of those types it sends an Ack `{"type":8,"sequenceId":<count received>}` within 1 s. A client
  Ack lower than one already received is ignored; one above the count sent is a protocol error.
- **Reconnecting.** When its WebSocket drops without a hub Close, the connection is kept for 30 s
  on the `TimeProvider`. A new upgrade with the same `id` by the same caller within that time
  resumes it: the client's handshake again, answered `{}`, then a Sequence
  `{"type":9,"sequenceId":<first unacknowledged>}` and every kept message from there in order. A
  client Sequence makes the server drop received messages numbered below it as duplicates. After
  30 s the connection ends and its token is `404`.
- **As built (BL-755).** The Ack is sent immediately after each numbered client message, which meets the
  1 s bound. Streams (`Count`) in progress when the WebSocket is lost are cancelled with it and not
  resumed; only records already numbered are replayed. A lost WebSocket is one that ends with no Close
  either way (the client vanished or the read failed); a hub Close or WebSocket Close ends the
  connection for good.
- **Bounded.** At most 100000 bytes of unacknowledged records a connection (ASP.NET Core's
  `StatefulReconnectBufferSize` default); more ends the connection with the protocol-error Close of
  decision 3.

### 7. Limits (ADR-0006)

| Limit | How the server applies it | Answer when hit |
| --- | --- | --- |
| connections (`--max-connections`, `--max-connections-per-address`) | `Surl.Core`'s refusal before HTTP | the connection refused |
| request head, slow sender (`--max-request-head`, `--head-timeout`) | `Http1ConnectionServer` (ADR-0084 decision 1) | its answer (`431`, `408` or close) |
| negotiate body (`--max-filesize`) | read and discarded | `Http1ConnectionServer`'s answer to a body past `--max-filesize` |
| hub connections: negotiated but not upgraded, plus upgraded | 1024 a hub (ADR-0006's connection ceiling) | negotiate: `{"error":"The server has too many connections."}`; an upgrade without `id`: `503 Service Unavailable` |
| a negotiated token not upgraded (`--head-timeout`, 30 s) | forgotten | `404` on its upgrade |
| handshake request (`--head-timeout`) | decision 3 | WebSocket Close `1008` |
| a WebSocket frame (`--max-message`, 1048576) | `Surl.WebSocket` | WebSocket Close `1009` |
| a hub record (`--max-message`) | bytes before its `0x1E`, counted across messages, judged before the rest is read | Close `{"type":7,"error":"Connection closed with an error."}`, WebSocket Close `1009` |
| `invocationId` and stream ID length | 128 characters (HUB "Invocations": a callee "may establish reasonable limits") | the protocol-error Close of decision 3 |
| invocations in progress a connection | 100 | Completion `Too many invocations in progress.` |
| an upload stream (`--max-filesize`, 104857600) | the items' JSON bytes for one `Collect` | Completion `The upload stream is too large.` |
| `Count`'s `n` | 0 to 1000 | Completion `Invalid arguments for the hub method.` |
| unsent records a connection | 1048576 bytes; a broadcast that would pass it is not queued for that connection, which is then closed | Close `{"type":7,"error":"Connection closed with an error."}`, WebSocket Close `1008` |
| keep-alive and client timeout | 15 s and 30 s (decision 4) | Ping; Close with the timeout text |
| `--idle-timeout` (120 s), `--max-time` (3600 s) | `Surl.Core` (ADR-0006) | the connection discarded |
| shutdown | each connection gets a Close `{"type":7}` then a WebSocket Close `1001`, within the grace (ADR-0059) | as said |

No new option is added: ADR-0006's options cover what a test harness tunes.

## Alternatives considered

- **WebSockets only, for good.** Rejected: a complete SignalR server offers the three transports TP
  describes, and a client configured for Server-Sent Events or Long Polling could not be tested.
  WebSockets lands first (BL-449) and BL-757 adds the others, the negotiate list growing with them.
- **Leave stateful reconnect and MessagePack out.** Rejected for the same reason; each is its own
  task (BL-755, BL-756) so BL-448 and BL-449 stay sized for one run.
- **Use the negotiate `error` form for a missing token.** Rejected: TP leaves the refusal to the
  server, a `401` is what ASP.NET Core's own authorization answers, and a status needs no body
  parsing; the `error` form is kept for the hub being full, which is not an HTTP failure.
- **Echo the client's protocol or method name in error text**, as ASP.NET Core does. Rejected:
  ADR-0006's fixed error text; the fixed texts say the same without repeating client bytes.
- **Treat unknown members as protocol errors.** Rejected: HUB "Errors" makes termination a "may",
  and ignoring them is what keeps a client that adds members working.
- **Offer `Text` only.** Rejected: the WebSocket transport carries both frame types, the server reads
  JSON from either, and BL-756's MessagePack needs `Binary`.
- **Put `Broadcast` on ADR-0100's message bus.** Rejected: ADR-0100 does not make SignalR a bus
  protocol, and a hub reaching only its own connections is what a SignalR hub's `Clients.All` does.
- **Options for the keep-alive and timeout.** Rejected for now: no test needs other values; a later
  ADR may add one.

## Consequences

- `surl signalr://127.0.0.1:5000/chat` (with an account, or `--allow-anonymous`) is a SignalR hub
  that negotiates (versions 0 and 1), upgrades to WebSockets, speaks the JSON hub protocol, echoes,
  broadcasts, streams, collects uploads, fails on request, keeps the connection alive and times out
  silent clients; `signalrs://` is the same inside TLS.
- BL-448 to BL-450 build it, BL-757 to BL-756 complete it, and the Curl port's client may connect to
  it as a live client in `Integration` tests (BL-451, ADR-0083).
- The requirement matrix [`SignalR.md`](../../Conformance/Extended/SignalR.md) traces each
  server-side requirement to BL-448, BL-449 or a later task; each task replaces its ID with the
  named test.
