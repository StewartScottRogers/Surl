# Surl.Extended.Protocol.SignalR.UnitLibrary

An extended server (ADR-0083): it answers the `signalr` and `signalrs` schemes the Curl port adds,
which upstream curl does not have. ADR-0119 decides how. It is judged by ASP.NET Core SignalR's
`TransportProtocols.md` and `HubProtocol.md` (dotnet/aspnetcore `main` at
`6aacb16a27a677d16278a9814ed04f871020854b`), never by the Curl port's output, and its unit tests
replay requests and records written by hand from those specifications' examples.

It references `Surl.Protocol.Abstractions`, `Surl.HttpMessage`, `Surl.HttpServing` (ADR-0084
decision 1) and `Surl.WebSocket` (ADR-0084 decision 2). JSON goes through
`Utf8JsonReader`/`Utf8JsonWriter` and `JsonDocument` only, so it stays native-AOT safe.

What is here so far (BL-448 and BL-449, ADR-0119 decisions 0 to 5 and most of 7):

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
  one held until its WebSocket ends (`TakeUp`, `End`, `SignalRUpgradeOutcome`).
- The WebSocket transport: `SignalRWebSocketTakeover` owns the connection after the `101` and runs a
  `SignalRHubSession` - the read loop (frames, records, messages), the clock (the handshake within
  `--head-timeout`, else WebSocket Close `1008`; a Ping after 15 s without a record sent; the
  timeout Close after 30 s without a record received) and one task per `Count` stream, writes one
  at a time, each record one text message. `SignalRSessionEnd` is how it ends: an optional hub
  Close, then a WebSocket Close code, or nothing when the client vanished or the exchange was
  cancelled.
- The hub's methods: `SignalRHubMethods` (`Echo`, `Count` up to 1000 items 100 ms apart, `Fail`,
  and the fixed error texts) answering each call as a `SignalRHubCall`; at most 100 streams in
  progress a connection.
- The JSON hub protocol: `HubRecordReader` cuts records at `0x1E` across buffers within
  `--max-message`; `HubMessageJsonReader` reads types 1 to 7 into `HubMessage` records or finds a
  protocol error; `HubMessageJsonWriter` writes them back, client values byte for byte;
  `HubHandshake` judges the handshake request and writes its response.

Still to come: `Broadcast`, `Collect` uploads, the shutdown farewell and the unsent-bytes cap
(BL-772; until then `Broadcast` and `Collect` are `Unknown hub method.`, a client StreamItem or
Completion is a protocol error, and a cancelled exchange writes nothing), the command line (BL-450),
Server-Sent Events and Long Polling (BL-757), stateful reconnect (BL-755) and MessagePack (BL-756).
