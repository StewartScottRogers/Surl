# Surl.Extended.Protocol.SignalR.UnitLibrary

An extended server (ADR-0083): it answers the `signalr` and `signalrs` schemes the Curl port adds,
which upstream curl does not have. ADR-0119 decides how. It is judged by ASP.NET Core SignalR's
`TransportProtocols.md` and `HubProtocol.md` (dotnet/aspnetcore `main` at
`6aacb16a27a677d16278a9814ed04f871020854b`), never by the Curl port's output, and its unit tests
replay requests and records written by hand from those specifications' examples.

It references `Surl.Protocol.Abstractions`, `Surl.HttpMessage` and `Surl.HttpServing` (ADR-0084
decision 1); BL-449 adds `Surl.WebSocket`. JSON goes through `Utf8JsonReader`/`Utf8JsonWriter`
and `JsonDocument` only, so it stays native-AOT safe.

What is here so far (BL-448, ADR-0119 decisions 0, 1, 2, 3, 5 and the record limits of 7):

- `SignalRProtocolServer` serves each connection through `Http1ConnectionServer` with a
  `SignalRRequestAnswerer`, starting the connection's `IHttpAuthenticationSession` first. The hub
  path is given to it as a `SignalRHubPath` (`ListenUrl` carries no path; BL-450 passes the listen
  URL's path).
- `SignalRRequestAnswerer` judges every request's login first (`access_token` in the query stands in
  for a missing `Authorization: Bearer`), answers `POST [endpoint-base]/negotiate` (versions 0 and 1,
  `400` for a bad `negotiateVersion`, `405 Allow: POST`, the `error` form when the hub is full),
  `426` with `Sec-WebSocket-Version: 13` for `GET [endpoint-base]` until BL-449 serves the upgrade,
  `405 Allow: GET` for other methods there, and `404` for every other path.
- `SignalRHubConnections` holds the negotiated connections of one hub: fresh 16-byte base64url
  `connectionId` and `connectionToken` from `RandomNumberGenerator`, at most 1024, each forgotten
  `--head-timeout` after its negotiate.
- The JSON hub protocol: `HubRecordReader` cuts records at `0x1E` across buffers within
  `--max-message`; `HubMessageJsonReader` reads types 1 to 7 into `HubMessage` records or finds a
  protocol error; `HubMessageJsonWriter` writes them back, client values byte for byte;
  `HubHandshake` judges the handshake request and writes its response.

Still to come: the WebSocket transport and hub methods (BL-449), the command line (BL-450), Server-Sent
Events and Long Polling (BL-757), stateful reconnect (BL-755) and MessagePack (BL-756).
