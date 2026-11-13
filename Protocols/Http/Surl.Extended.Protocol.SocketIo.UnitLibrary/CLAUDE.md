# Surl.Extended.Protocol.SocketIo.UnitLibrary

An extended server (ADR-0083): it answers the `socketio` and `socketios` schemes the Curl port
adds, which upstream curl does not have. ADR-0120 decides how. It is judged by the Engine.IO protocol,
4th version (github.com/socketio/engine.io-protocol `main` at `f21de7b00ed09b3bbad2807db718ea5d6bc36aba`)
and the Socket.IO protocol, 5th version (github.com/socketio/socket.io-protocol `main` at
`4f633dd6a4445492d57af3b27ddd2d336014f1f1`), never by the Curl port's output, and its unit tests replay
packets written by hand from those specifications' examples.

It references `Surl.Protocol.Abstractions`, `Surl.HttpMessage`, `Surl.HttpServing` (ADR-0084
decision 1) and `Surl.WebSocket` (ADR-0084 decision 2). JSON goes through
`Utf8JsonReader`/`Utf8JsonWriter` and `JsonDocument` only, so it stays native-AOT safe.

The server (BL-454 and BL-762, ADR-0120 decisions 1 to 7 and amendments 1 and 2), over HTTP
long-polling and the WebSocket:

- `SocketIoProtocolServer` serves each connection through `Http1ConnectionServer` with a
  `SocketIoRequestAnswerer`. It is given its `SocketIoServedNamespaces` (`/` and the listen URL's
  path; `ListenUrl` carries no path, so BL-455 passes it), its listener's `SocketIoSessions` and the
  `IAuthenticationPolicy`; the three travel together as the internal `SocketIoListener`.
- `SocketIoRequestAnswerer` answers `/socket.io/` only (`404` elsewhere), refuses a bad query `400`
  with `EngineIoRefusal`'s coded bodies (transport 0, unknown `sid` 1, method 2, `EIO` 5, no upgrade
  or a session already on or probing a WebSocket 3), written by `SocketIoAnswers`. Polling requests
  go to `SocketIoPollingAnswerer`; a WebSocket request goes through `Surl.WebSocket`'s checks, then
  `503` past `SocketIoSessions.MaxSessions` (1024) for a new session, and its `101` hands the
  connection to `SocketIoWebSocketTakeover`. `SocketIoRequestTarget` reads the query.
- `SocketIoPollingAnswerer` opens a polling session (the open packet with `upgrades`
  `["websocket"]`, `text/plain; charset=UTF-8`), answers a held `GET` with every queued packet joined
  by `0x1E` (`6` during an upgrade, `1` once ended), and reads a `POST` within `--max-message` into a
  `SocketIoPollingBodyBuffer` (`413` past it; code 3 for a malformed body or a second concurrent
  `GET` or `POST`, which closes the session), answering `text/html` `ok`. A polling session lives on
  no connection: its heartbeat, connect timeout or shutdown ends it.
- `SocketIoSession` is one Engine.IO session whatever carries it (`SocketIoTransportState`): the open
  packet (`maxPayload` `--max-message`), Engine.IO packets in (Socket.IO packets and the attachments
  a `SocketIoPendingBinaryPacket` collects), the clock (ping every 25 s, end without a pong within
  20 s, end with no namespace for `--head-timeout`), CONNECT judged through the policy from
  `SocketIoConnectCredentials`, echo, `broadcast` and ACK (`SocketIoEventPayload` reads the name and
  the arguments), its polls, posts and upgrade state, and its end as a `SocketIoSessionEnd`.
  Everything it sends goes through its `SocketIoOutbox` of `SocketIoOutboxItem`s, in order, at most
  1048576 bytes unsent (else close `1008`).
- `SocketIoWebSocketTransport` carries a session over a WebSocket: frames to packets and back, the
  probe of a polling session's WebSocket (`2probe` answered `3probe`, then `5`), and the end's Close;
  a limit's cancellation gets a DISCONNECT per namespace then Close `1001`; shutdown gets nothing.

The codecs (BL-453, ADR-0120 decisions 0, 3 and 6 and the packet limits of 7):

- Engine.IO v4: `EngineIoPacket` and `EngineIoPacketType`; `EngineIoPacketCodec` reads and writes a
  packet as a WebSocket message (text: type character then payload; binary: the bare bytes) and as one
  packet of a polling payload (binary as `b` plus base64), refusing an empty message or a type
  character outside `0` to `6`; `EngineIoPollingPayload` joins and splits packets at `0x1E`;
  `EngineIoOpenPacket` writes the open packet in EIO "Handshake"'s member order.
- Socket.IO v5: `SocketIoPacket` and `SocketIoPacketType`; `SocketIoPacketWriter` writes
  `<type>[<attachments>-][<namespace>,][<ack id>][<JSON payload>]`; `SocketIoPacketReader` reads it and
  refuses a malformed packet (type outside `0` to `6`, attachment count missing or outside 1 to 64, a
  namespace without `,`, over 128 bytes or not UTF-8, an ack id over 15 digits, an ACK without one) and,
  through `TryReadFromClient`, a client CONNECT_ERROR; `SocketIoPayloadCheck` judges the payload's JSON
  per type (nesting at most 64; CONNECT absent or an object, DISCONNECT none, EVENT a non-empty array
  with a string first, ACK an array, CONNECT_ERROR an object) and, through the internal
  `SocketIoPlaceholderCheck`, a binary packet's placeholders.
- `SocketIoRandomId` makes the 20-character base64url ids for an Engine.IO `sid` and a namespace
  connection's `sid`.

Still to come: the command line (BL-455).

Faults (BL-1064, ADR-0124 decisions 4 and 9): `SocketIoFaults` asks `ExchangeContext.Faults` for each namespace
CONNECT after its login is accepted (`command` `connect`) and each event of a connected namespace (`command` the
event name), with `path` the namespace and `account` the login's user name. A delay holds the packet; a drop ends
the session silently; an `error` rule with a reply answers a CONNECT with a CONNECT_ERROR whose `message` is the
text, and an event that carried an ack id with an ACK holding `[{"error":"text"}]` (an event with no ack id is
swallowed, as nothing can carry the error back). Each publishes a `FaultAppliedEvent`; any other rule is noted and
the packet is served as without it.
