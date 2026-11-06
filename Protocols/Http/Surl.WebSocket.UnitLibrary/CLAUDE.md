# Surl.WebSocket.UnitLibrary

A horizontal library: RFC 6455 itself, shared by every server that speaks WebSocket -
`Surl.Protocol.Ws` today, and the extended servers SignalR, SocketIo and Mqtt5's `mqtt5+ws`
as they are built
([ADR-0084](../../../Documentation/Planning/Decisions/ADR-0084-the-libraries-the-extended-servers-share-for-http-1-1-websocket-and-dtls.md)
decision 2). The WebSocket server's behaviour is
[ADR-0071](../../../Documentation/Planning/Decisions/ADR-0071-how-the-websocket-server-answers-upstream-curl.md)'s;
nothing here decides what a server serves.

What it holds, all in namespace `Surl.WebSocket`, every type `public`:

- The opening handshake: `WebSocketUpgradeChecks` (ADR-0071 decision 1's checks 4 to 9),
  `WebSocketUpgradeRefusal`, `WebSocketHttpStatuses` (`101` and `426`), `WebSocketAcceptKey`, and
  `WebSocketUpgradeAcceptance`, which builds the `101` and chooses a subprotocol from the
  server's own list (RFC 6455 section 4.2.2). It never refuses on a subprotocol and negotiates
  no extension.
- The frame codec, written from RFC 6455 alone: `WebSocketFrameReader` (with
  `WebSocketFrameReadResult` and `WebSocketFrameReadOutcome`), `WebSocketFrameEncoder`,
  `WebSocketMessageReassembler` (with `WebSocketReassemblyStep` and
  `WebSocketReassemblyOutcome`), `WebSocketFrame`, `WebSocketMessage`, `WebSocketOpcode`,
  `WebSocketOpcodes` and `WebSocketCloseCodes`.
- `WebSocketLingeringClose`: the wait for the client's close after the server's.

It references `Surl.Protocol.Abstractions.UnitLibrary` and `Surl.HttpMessage.UnitLibrary`
only - its row in `ProtocolIsolationTests.HorizontalLibraries` - and never a protocol server
or `Surl.HttpServing`. It constructs no socket: every read and write goes through the
`IConnection` it is given, so its tests run on `InMemoryConnection` with no network.

Its tests replay RFC 6455's own examples (section 5.7, and section 1.3's handshake key). The
bytes pinned upstream curl sends and expects are proven against the WebSocket server in
`Surl.Protocol.Ws.UnitTests` and `Surl.Conformance.UnitTests`, never from the Curl port (ADR-0003).
