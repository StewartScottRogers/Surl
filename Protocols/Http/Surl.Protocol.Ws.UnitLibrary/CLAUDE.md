# Surl.Protocol.Ws.UnitLibrary

Phase 4, built.

The WebSocket server (RFC 6455) as
[ADR-0071](../../../Documentation/Planning/Decisions/ADR-0071-how-the-websocket-server-answers-upstream-curl.md)
decides it: it answers the HTTP/1.1 upgrade upstream curl sends, then sends the file or listing
at the request path, or echoes client messages under `--ws-echo`, and answers every client
frame, including `PING`, `PONG` and `CLOSE`. What it answers, and what pinned upstream curl has
proven against it, is in `Documentation/Product/Product-Overview.md`, "Built for Phase 4:
WebSocket"; the terms are in `Documentation/Wiki/Glossary.md`, section "WebSocket".

**URL schemes answered:** `ws`. `WsProtocolServer.Schemes` claims `ws` only; `wss` is the same
server registered in `Surl.Composition`'s `ComposeProtocolServers` behind `ImplicitTlsSchemeServer`,
over a connection already secured, and the server never tells the two apart by scheme.

What it holds, all in namespace `Surl.Protocol.Ws`:

- `WsProtocolServer` - the one public type: the `IConnectionProtocolServer` that reads each
  upgrade head with `Surl.HttpMessage`'s `HttpConnectionReader`.
- The upgrade (ADR-0071 decisions 1 to 3): `WebSocketUpgradeResponder` (the checks in order,
  the login through `IHttpAuthenticationSession`, the `101` - built by `Surl.WebSocket`'s
  `WebSocketUpgradeAcceptance` with no subprotocol - and every refusal) and
  `WebSocketServedEntry`.
- The exchange after the `101` (decisions 4 to 7): `WebSocketExchange`.

Every type but `WsProtocolServer` is `internal`, visible to `Surl.Protocol.Ws.UnitTests` only.
The handshake checks, the frame codec, reassembly, close codes and the lingering close are in
`Protocols/Http/Surl.WebSocket.UnitLibrary`
([ADR-0084](../../../Documentation/Planning/Decisions/ADR-0084-the-libraries-the-extended-servers-share-for-http-1-1-websocket-and-dtls.md)
decision 2), which the extended servers share.

This library references `Surl.Protocol.Abstractions.UnitLibrary`, `Surl.Content.UnitLibrary`
(the content store a request path is looked up and read in), `Surl.HttpMessage.UnitLibrary`
(the upgrade head read and the response heads written,
[ADR-0070](../../../Documentation/Planning/Decisions/ADR-0070-the-http-message-library-the-http-websocket-and-rtsp-servers-share.md))
and `Surl.WebSocket.UnitLibrary` (RFC 6455 itself, ADR-0084), all four horizontal libraries of
ADR-0002 decision 3's table as later ADRs amend it, and may reference no other project. Referencing another protocol server - `Surl.Protocol.Http`
included - is a build break, and `Surl.Protocol.Abstractions.UnitTests` fails if one appears.

Never construct a `Socket`, `TcpListener`, `UdpClient`, `SslStream` or `HttpListener`
here. The server receives its transport from the listener seam in
`Surl.Protocol.Abstractions`, so the tests in the matching `.UnitTests` project can drive
it with request bytes measured from pinned upstream curl, with no network.

Expected bytes come from a build pinned in `UpstreamCurlBuilds.json`, measured with
`Record-CurlExchange.ps1`, and never from the Curl port (ADR-0003). This project's fixtures
were recorded from the reference `curl.exe`, and how each was recorded is in
`Protocols/Http/Surl.Protocol.Ws.UnitTests/Fixtures/README.md`; the frames only libcurl's API sends were
measured through the pinned `libcurl-4.dll` (ADR-0071 decision 10 and Amendment 1), and
`Surl.Conformance.UnitTests` holds what it proves against `surl`.
