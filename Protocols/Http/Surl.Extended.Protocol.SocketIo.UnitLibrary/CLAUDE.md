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

The server (BL-454, ADR-0120 decisions 1 to 7 and amendment 1), over the WebSocket transport:

- `SocketIoProtocolServer` serves each connection through `Http1ConnectionServer` with a
  `SocketIoRequestAnswerer`. It is given its `SocketIoServedNamespaces` (`/` and the listen URL's
  path; `ListenUrl` carries no path, so BL-455 passes it), its listener's `SocketIoSessions` and the
  `IAuthenticationPolicy`; the three travel together as the internal `SocketIoListener`.
- `SocketIoRequestAnswerer` answers `/socket.io/` only (`404` elsewhere), refuses a bad query `400`
  with `EngineIoRefusal`'s coded bodies (transport 0, `sid` 1 or 3, method 2, `EIO` 5, no upgrade
  3), then `Surl.WebSocket`'s checks, then `503` past `SocketIoSessions.MaxSessions` (1024); the
  `101` hands the connection to `SocketIoWebSocketTakeover`. `SocketIoRequestTarget` reads the query.
- `SocketIoSession` runs one Engine.IO session: the open packet (`upgrades` `[]`, `maxPayload`
  `--max-message`), the read loop (frames, Engine.IO packets, Socket.IO packets and the attachments
  a `SocketIoPendingBinaryPacket` collects), the clock (ping every 25 s, close `1000` without a pong
  within 20 s, close `1000` with no namespace for `--head-timeout`), CONNECT judged through the policy
  from `SocketIoConnectCredentials`, echo, `broadcast` and ACK (`SocketIoEventPayload` reads the name
  and the arguments), and its end as a `SocketIoSessionEnd`. Everything it sends goes through its
  `SocketIoOutbox`, in order, at most 1048576 bytes unsent (else close `1008`). A limit's
  cancellation gets a DISCONNECT per namespace then Close `1001`; shutdown gets nothing.

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

Still to come: the command line (BL-455) and HTTP long-polling with its upgrade (BL-762; until
then `transport=polling` is code 0).
