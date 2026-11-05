# Socket.IO requirement matrix

The server-side requirements `Surl.Extended.Protocol.SocketIo` answers, for the `socketio` and
`socketios` schemes (ADR-0083 decision 9). Written from the Engine.IO protocol, 4th version (cited as
EIO; github.com/socketio/engine.io-protocol `main`, commit
`f21de7b00ed09b3bbad2807db718ea5d6bc36aba`), the Socket.IO protocol, 5th version (cited as SIO;
github.com/socketio/socket.io-protocol `main`, commit `4f633dd6a4445492d57af3b27ddd2d336014f1f1`),
both read 2026-11-05, and RFC 6455 for the WebSocket transport: the specifications
[ADR-0120](../../Planning/Decisions/ADR-0120-how-the-socket-io-server-answers-socketio-and-socketios.md)
decides by, never the Curl port's client matrix. Each row binds the server; "Covered by" names the
task that proves it, until that task replaces its ID with the named test, or says why the row needs
no test here.

IDs are `SocketIo:EIO-<section>-<n>` and `SocketIo:SIO-<section>-<n>` for the specifications, and
`SocketIo:Surl-<decision>-<n>` for a rule ADR-0120 adds where the specifications leave the server a
choice. Client-only requirements (sending the `sid` on later requests, at most one `GET` and one
`POST` at a time, choosing when to upgrade, considering the connection closed when no ping arrives)
are not rows; the server's answer to a client that breaks one is. Requirements of the HTTP
long-polling transport, which BL-762 adds, are rows marked "n/a" naming that task, and the task turns
each into a row of its own when it lands.

## Packet encoding (EIO "Protocol", "Packet encoding"; SIO "Exchange protocol", "Packet encoding")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| SocketIo:EIO-Encoding-1 | MUST | An Engine.IO packet is its type character `0` to `6` then its payload; over WebSocket each packet is one message, text payloads in text messages and binary `message` payloads in binary messages with no type prefix. | BL-453 |
| SocketIo:SIO-Encoding-1 | MUST | A Socket.IO packet is `<type>[<attachments>-][<namespace>,][<ack id>][<JSON payload>]`, the namespace only when it is not `/`; each SIO "Examples" encoding is read and written byte for byte (`0`, `0/admin,{"sid":"oSO0OpakMV_3jnilAAAA"}`, `4{"message":"Not authorized"}`, `2["foo"]`, `2/admin,["bar"]`, `212["foo"]`, `3/admin,13["bar"]`, `1`, `1/admin,`). | BL-453 |
| SocketIo:SIO-Encoding-2 | MUST | Binary packets carry their attachment count: `51-["baz",{"_placeholder":true,"num":0}]`, `52-/admin,["baz",{"_placeholder":true,"num":0},{"_placeholder":true,"num":1}]`, `61-15["bar",{"_placeholder":true,"num":0}]`. | BL-453 |
| SocketIo:Surl-6-1 | MUST | A binary packet with a count below 1, a placeholder `num` out of range or used twice, or a placeholder object with other members, is refused as malformed (ADR-0120 decision 6). | BL-453 |
| SocketIo:Surl-2-1 | MUST | A text message that is empty, or whose type character is outside `0` to `6`, is refused as malformed (ADR-0120 decision 2). | BL-453 |

## Path, version and the WebSocket transport (EIO "Request path", "Query parameters", "WebSocket", "Handshake"; RFC 6455)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| SocketIo:EIO-Path-1 | MUST | The Engine.IO path is `/socket.io/`; any other path is `404` with no body (ADR-0120 decision 1). | BL-454 |
| SocketIo:EIO-Query-1 | MUST | A request whose `EIO` is absent or not `4` is answered `400` `{"code":5,"message":"Unsupported protocol version"}` before any upgrade. | BL-454 |
| SocketIo:EIO-Query-2 | MUST | A request whose `transport` is absent or unknown is answered `400` `{"code":0,"message":"Transport unknown"}`; until BL-762, `polling` too. | BL-454 |
| SocketIo:Surl-1-1 | MUST | A request without `sid` whose method is not `GET` is `400` `{"code":2,"message":"Bad handshake method"}`; a `sid` no session has is `400` `{"code":1,"message":"Session ID unknown"}` (ADR-0120 decision 1). | BL-454 |
| SocketIo:EIO-Handshake-1 | MUST | `GET /socket.io/?EIO=4&transport=websocket` is upgraded (`101`) and opens a WebSocket-only session. | BL-454 |
| SocketIo:EIO-WebSocket-1 | MUST | A second WebSocket for a session that has one is refused `400` `{"code":3,"message":"Bad request"}`. | BL-454 |
| SocketIo:RFC6455-4.2-1 | MUST | The upgrade request is checked and refused as `Surl.WebSocket` does, with no subprotocol chosen (ADR-0071, ADR-0084 decision 2). | n/a: `Surl.WebSocket` owns the checks and is tested there (BL-408) |
| SocketIo:Surl-0-1 | MUST | `socketios` is the same server inside TLS from the first byte; without `--cert` or `--self-signed` the listener fails with `CertificateProblem` (58). | n/a: the listener seam wraps TLS before the server reads a byte (ADR-0010); BL-455 registers `socketios` and proves it |
| SocketIo:EIO-Polling-1 | MUST | HTTP long-polling: the handshake `GET`, a held `GET` answered with queued packets joined by `\x1e`, a `POST` answered `200` `ok`, binary as `b`-prefixed base64 and `Content-Type: application/octet-stream` read as binary. | n/a: BL-762 adds the transport (ADR-0120 decision 2) |
| SocketIo:EIO-Polling-2 | MUST | An unknown `sid` on a poll or post is `400`; a second concurrent `GET` or `POST` is `400` and closes the session. | n/a: BL-762 adds the transport (ADR-0120 decision 2) |
| SocketIo:EIO-Upgrade-1 | MUST | The upgrade from polling: `2probe` answered `3probe`, a held `GET` answered `6` (noop), and `5` moving the session to the WebSocket. | n/a: BL-762 adds the upgrade (ADR-0120 decision 2) |

## The open packet and the heartbeat (EIO "Handshake", "Heartbeat")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| SocketIo:EIO-Open-1 | MUST | The session's first packet is `open`: `0{"sid":"<20 characters>","upgrades":[],"pingInterval":25000,"pingTimeout":20000,"maxPayload":<--max-message>}`, the `sid` 15 random bytes in base64url (ADR-0120 decision 3). | BL-454 |
| SocketIo:Surl-2-2 | MUST | `upgrades` is `[]` on a WebSocket session, and on every session until BL-762 (ADR-0120 decision 2). | BL-454 |
| SocketIo:EIO-Heartbeat-1 | MUST | The server sends `2` `pingInterval` after the open packet and after each pong, on the injected `TimeProvider`. | BL-454 |
| SocketIo:EIO-Heartbeat-2 | SHOULD | No `3` within `pingTimeout` of a `2` closes the session, WebSocket Close `1000`. | BL-454 |
| SocketIo:Surl-2-3 | MUST | A client `1` ends the session (WebSocket Close `1000`); a client `2` other than `2probe`, a pong with no ping outstanding, and `6` are ignored; a client `0`, or `5` outside an upgrade, closes the WebSocket `1002` (ADR-0120 decisions 2 and 3). | BL-454 |

## Namespaces and authentication (SIO "Connection to a namespace", "History"; ADR-0120 decision 4)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| SocketIo:SIO-Connect-1 | MUST | A session whose first Socket.IO packet is not a CONNECT is closed. | BL-454 |
| SocketIo:SIO-Connect-2 | MUST | A CONNECT that is allowed is answered with a CONNECT carrying `{"sid":"…"}` on the same namespace (`40{"sid":"…"}`, `40/admin,{"sid":"…"}`), the id fresh and independent of the Engine.IO `sid`. | BL-454 |
| SocketIo:SIO-Connect-3 | MUST | A CONNECT that is not allowed is answered with a CONNECT_ERROR whose payload is an object with `message`. | BL-454 |
| SocketIo:Surl-4-1 | MUST | The namespaces served are `/` and the listen URL's path; any other is CONNECT_ERROR `{"message":"Invalid namespace"}`. | BL-454 |
| SocketIo:SIO-Connect-4 | MAY | A client may connect several namespaces over one session; a CONNECT for one already connected is ignored (ADR-0120 decision 4). | BL-454 |
| SocketIo:Surl-4-2 | MUST | Without `--allow-anonymous`, a CONNECT whose payload carries no `token` and no `username` and `password` is CONNECT_ERROR `{"message":"Not authorized"}`. | BL-454 |
| SocketIo:Surl-4-3 | MUST | `token` is judged as the empty-name account's password and `username`/`password` as that account's login, through `IAuthenticationPolicy.CheckPasswordLoginAsync` with the connection's `TlsSession`; a refusal is CONNECT_ERROR `{"message":"Not authorized"}` after the refusal delay, naming nothing. | BL-454 |
| SocketIo:Surl-4-4 | MUST | A password over plain `socketio` is refused without `--allow-plaintext-auth`; `socketios` logs in by default. | BL-454 |
| SocketIo:Surl-4-5 | MUST | A CONNECT payload that is present and not a JSON object is a protocol error. | BL-454 |
| SocketIo:Surl-4-6 | MUST | A session with no namespace connected for `--head-timeout` (from its open packet or its last DISCONNECT) is closed, WebSocket Close `1000`. | BL-454 |
| SocketIo:Surl-4-7 | MUST | An `Authorization` field on the upgrade is ignored; the login is the CONNECT payload's alone. | BL-454 |

## Events, acknowledgements and disconnection (SIO "Sending and receiving data", "Acknowledgement", "Disconnection from a namespace")

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| SocketIo:SIO-Event-1 | MUST | An EVENT whose payload is not a non-empty array with a string first is a protocol error that closes the connection. | BL-454 |
| SocketIo:Surl-5-1 | MUST | An EVENT without an ack id is echoed to its sender on the same namespace, its array byte for byte (`42["hello",1]` answered `42["hello",1]`). | BL-454 |
| SocketIo:Surl-5-2 | MUST | An EVENT named `broadcast` is sent to every session connected to that namespace on the listener, its sender included, once each. | BL-454 |
| SocketIo:Surl-5-3 | MUST | An EVENT for a namespace the session has not connected is dropped. | BL-454 |
| SocketIo:SIO-Ack-1 | MUST | An EVENT with an ack id is answered by an ACK with the same id; its payload is the event's arguments, an array possibly empty (`421["hello",1]` answered `431[1]`), and the event is not echoed. | BL-454 |
| SocketIo:Surl-5-4 | MUST | A client ACK or BINARY_ACK (the server asks for none) is read, attachments included, and dropped. | BL-454 |
| SocketIo:SIO-Disconnect-1 | MUST | A client DISCONNECT leaves its namespace with no answer; the session stays while another namespace is connected. | BL-454 |
| SocketIo:Surl-5-5 | MUST | A client CONNECT_ERROR, or a Socket.IO packet type outside 0 to 6, is a protocol error. | BL-453 |

## Binary attachments (SIO "Packet encoding"; ADR-0120 decision 6)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| SocketIo:SIO-Binary-1 | MUST | A BINARY_EVENT or BINARY_ACK is followed by its N attachments as binary `message` packets, which belong to it and are not packets of their own; a text packet before they have all arrived is a protocol error. | BL-454 |
| SocketIo:SIO-Binary-2 | MUST | A BINARY_EVENT is echoed (or broadcast) as a BINARY_EVENT with the same placeholders and the same attachments in order, byte for byte. | BL-454 |
| SocketIo:SIO-Binary-3 | MUST | A BINARY_EVENT with an ack id is answered by a BINARY_ACK with the same id, count and attachments. | BL-454 |

## Limits and shutdown (ADR-0120 decision 7)

| ID | Level | Requirement | Covered by |
| --- | --- | --- | --- |
| SocketIo:EIO-MaxPayload-1 | MUST | An Engine.IO packet over `maxPayload` (`--max-message`) closes the session, WebSocket Close `1009`. | BL-454 |
| SocketIo:Surl-7-1 | MUST | More than 64 attachments in one packet is a protocol error (`1002`); a text part and attachments together over `--max-message` close the session `1009`. | BL-454 |
| SocketIo:Surl-7-2 | MUST | A namespace name over 128 bytes, an ack id over 15 digits, or JSON nested over 64 levels is a protocol error. | BL-453 |
| SocketIo:Surl-7-3 | MUST | 1024 sessions a listener; the next new session's upgrade is `503` with no body. | BL-454 |
| SocketIo:Surl-7-4 | MUST | A session with more than 1048576 bytes unsent is closed, WebSocket Close `1008`. | BL-454 |
| SocketIo:Surl-7-5 | MUST | Shutdown sends each connected namespace a DISCONNECT, then closes the WebSocket `1001`, within the grace (ADR-0059). | BL-454 |
