# Surl.Extended.Protocol.SocketIo.UnitLibrary

An extended server (ADR-0083): it will answer the `socketio` and `socketios` schemes the Curl port
adds, which upstream curl does not have. ADR-0120 decides how. It is judged by the Engine.IO protocol,
4th version (github.com/socketio/engine.io-protocol `main` at `f21de7b00ed09b3bbad2807db718ea5d6bc36aba`)
and the Socket.IO protocol, 5th version (github.com/socketio/socket.io-protocol `main` at
`4f633dd6a4445492d57af3b27ddd2d336014f1f1`), never by the Curl port's output, and its unit tests replay
packets written by hand from those specifications' examples.

It references `Surl.Protocol.Abstractions` only so far; BL-454 adds `Surl.HttpServing` and
`Surl.WebSocket` when it serves sessions. JSON goes through `Utf8JsonReader`/`Utf8JsonWriter` only, so
it stays native-AOT safe.

What is here so far (BL-453, ADR-0120 decisions 0, 3 and 6 and the packet limits of 7) - the codecs,
with no transport:

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

Still to come: sessions over the WebSocket transport, namespaces, authentication, events, acks,
attachments in flight, the heartbeat and the limits (BL-454), the command line (BL-455) and HTTP
long-polling with its upgrade (BL-762).
