# Surl.Extended.Protocol.SignalR.UnitLibrary

An extended server (ADR-0083): it answers the `signalr` and `signalrs` schemes the Curl port adds,
which upstream curl does not have. ADR-0119 decides how. It is judged by ASP.NET Core SignalR's
`TransportProtocols.md` and `HubProtocol.md` (dotnet/aspnetcore `main` at
`6aacb16a27a677d16278a9814ed04f871020854b`), never by the Curl port's output, and its unit tests
replay requests and records written by hand from those specifications' examples.

It references `Surl.Protocol.Abstractions`, `Surl.HttpMessage`, `Surl.HttpServing` (ADR-0084
decision 1) and `Surl.WebSocket` (ADR-0084 decision 2). JSON goes through
`Utf8JsonReader`/`Utf8JsonWriter` and `JsonDocument` only, so it stays native-AOT safe.

What is here so far (BL-448, BL-449 and BL-772, ADR-0119 decisions 0 to 5 and 7, and its Amendment 1):

- `SignalRProtocolServer` serves each connection through `Http1ConnectionServer` with a
  `SignalRRequestAnswerer`, starting the connection's `IHttpAuthenticationSession` first. The hub
  path is given to it as a `SignalRHubPath` (`ListenUrl` carries no path; BL-450 passes the listen
  URL's path).
- `SignalRRequestAnswerer` judges every request's login first (`access_token` in the query stands in
  for a missing `Authorization: Bearer`), answers `POST [endpoint-base]/negotiate` (versions 0 and 1,
  `400` for a bad `negotiateVersion`, `405 Allow: POST`, the `error` form when the hub is full),
  the WebSocket upgrade of `[endpoint-base]` (`Surl.WebSocket`'s checks in ADR-0071's order, so a
  `GET` without an upgrade is `426` and another method `405 Allow: GET`; then the `id`: `101` for a
  connection the same caller negotiated or a new one without `id`, `404` unknown, `409` already
  taken up, `403` another caller, `503` a full hub), and `404` for every other path.
- `SignalRHubConnections` holds the connections of one hub: fresh 16-byte base64url
  `connectionId` and `connectionToken` from `RandomNumberGenerator`, at most 1024 negotiated and
  upgraded together, a negotiated one forgotten `--head-timeout` after its negotiate, an upgraded
  one held until its WebSocket ends (`TakeUp`, `End`, `SignalRUpgradeOutcome`). It also holds the
  sessions past their handshake (`Join`, `Leave`) and hands each `Broadcast` record to every one of
  them as an `ISignalRBroadcastReceiver`.
- The WebSocket transport: `SignalRWebSocketTakeover` owns the connection after the `101` and runs a
  `SignalRHubSession` - the read loop (frames, records, messages, `Collect` uploads), the clock (the
  handshake within `--head-timeout`, else WebSocket Close `1008`; a Ping after 15 s without a record
  sent; the timeout Close after 30 s without a record received), one task per `Count` stream and
  one per broadcast queued for it, writes one at a time in the order queued, each record one text
  message. More than 1048576 bytes queued and unwritten (`MaxUnsentBytes`) closes it with the
  protocol-error Close and `1008`. `SignalRSessionEnd` is how it ends: an optional hub Close, then a
  WebSocket Close code; nothing when the client vanished; and the farewell (`{"type":7}`, then
  `1001`, on its own one-second deadline) when the exchange is cancelled at shutdown or at a limit.
- The hub's methods: `SignalRHubMethods` (`Echo`, `Broadcast`, `Count` up to 1000 items 100 ms
  apart, `Collect`, `Fail`, and the fixed error texts) answering each call as a `SignalRHubCall`;
  at most 100 streams and uploads in progress a connection. `SignalRUpload` is one `Collect`'s
  upload: its items counted against `--max-filesize` and kept for the Completion's array, or
  dropped once the `Collect` is completed early.
- The JSON hub protocol: `HubRecordReader` cuts records at `0x1E` across buffers within
  `--max-message`; `HubMessageJsonReader` reads types 1 to 7 into `HubMessage` records or finds a
  protocol error; `HubMessageJsonWriter` writes them back, client values byte for byte;
  `HubHandshake` judges the handshake request and writes its response.

Still to come: the command line (BL-450).

**Transports** (BL-757, ADR-0119 decision 2 and amendment 1): `SignalRHubSession` runs the hub over an
`ISignalRHubTransport` (`ReadAsync` for the client's next payload, `WriteRecordAsync`, `WriteEndAsync`, a `WriteGate`
the session holds around writes) and knows nothing of what carries it. `SignalRWebSocketTransport` is the frames, the
Pong and the WebSocket Close; `SignalRServerSentEventsTransport` writes each record as an event on the open
`HttpAnswerBody.Streamed` response (`Text` only: a MessagePack handshake is refused); `SignalRLongPollingTransport`
queues records for `PollAsync` (`SignalRPollResult`: `200` with the records or, after 90 s, none; `204` for a
superseded poll or an ended connection) and `Delete`. The client's half of both is HTTP Post: `SignalRPostedPayloads`
hands each body to the session and completes its request when the session asks for the next payload; a second
outstanding post is `409`. `SignalRRequestAnswerer` (its `.HttpTransports.cs` part) answers every request to
`[endpoint-base]` without `Upgrade: websocket`: `id` mandatory (`400`), `404` unknown, `403` another account's,
`400` on another transport's connection; `SignalRHttpTransportState`, set on the `SignalRNegotiatedConnection` when a
transport takes it up, is how a later request finds its posts and poll. A Long Polling session outlives its first
request, so it runs on `ExchangeContext.ShutdownToken` alone. Stateful reconnect stays WebSockets only.

**Stateful reconnect** (BL-755, ADR-0119 decision 6): a negotiate with `useStatefulReconnect=true` or
`useAck=true` is answered with `"useStatefulReconnect":true` and gives the connection a
`SignalRReconnectState`. On such a connection the session numbers each Invocation, StreamItem and
Completion it sends and keeps it until a client Ack covers it (at most 100000 bytes, else the
protocol-error Close); it Acks each numbered client message at once; a client Sequence makes it drop
the repeats. A WebSocket lost with no Close is `Suspend`ed in `SignalRHubConnections` for 30 s, and an
upgrade with the same `id` and caller resumes it: handshake `{}`, a Sequence, then the kept records.
Streams in progress when the WebSocket is lost are not resumed. Types 8 and 9 stay protocol errors on
a connection that did not ask.

**Chosen-error fault** (ADR-0144, BL-835): `SignalRWebSocketTakeover` gives each `SignalRHubSession` a
`SignalRInvocationFaults` (the hub's endpoint base and the negotiating account); every Invocation or
StreamInvocation with an `invocationId` is offered to it before the hub method, `command` the target. A matching
`error` rule's text completes it as the Completion's `error` (no StreamItem), publishing `FaultAppliedEvent`; a
non-blocking Invocation is never offered.

**MessagePack hub protocol** (BL-756, ADR-0119 decision 3): a handshake of `{"protocol":"messagepack","version":1}` is
answered `{}` (still JSON, a text message) and `SignalRHubSession` then reads and writes binary WebSocket messages.
`MessagePackCursor` reads the subset the hub needs (every format, skipped without recursion), `MessagePackWriter`
writes it in shortest forms, `HubMessagePackRecordReader` cuts messages by their VarInt length across WebSocket
messages within `--max-message`, `HubMessagePackReader` and `HubMessagePackWriter` read and write types 1 to 9.
A `HubJsonValue` on such a connection holds one MessagePack value's bytes (its member is still named `Utf8Json`).
`SignalRHubMethods.Call` takes `isMessagePack` to read `Count`'s argument. Bytes after a MessagePack handshake in the
same WebSocket message are read as MessagePack. A connection that resumes (BL-755) with another protocol than it
began with is not guarded: its kept records stay in the first encoding.

**Command-stage delay and drop** (ADR-0144 amendment 1, BL-943): `SignalRInvocationFaults.ApplyAsync` also applies a `delay` rule (holds the Completion) and a `drop` rule (`SignalRSessionEnd.Silent`: the connection ends, no Completion, no Close). Non-blocking Invocations are still never offered.
