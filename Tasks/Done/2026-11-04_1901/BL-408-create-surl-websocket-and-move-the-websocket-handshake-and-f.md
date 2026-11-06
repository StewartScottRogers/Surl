---
id: BL-408
title: Create Surl.WebSocket and move the WebSocket handshake and framing out of Surl.Protocol.Ws
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-401, BL-404]
touches: [Protocols/Http/Surl.WebSocket.UnitLibrary, Protocols/Http/Surl.WebSocket.UnitTests, Protocols/Http/Surl.Protocol.Ws.UnitLibrary, Protocols/Http/Surl.Protocol.Ws.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitTests, Surl.slnx, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-408 — Create Surl.WebSocket and move the WebSocket handshake and framing out of Surl.Protocol.Ws

## Goal

The RFC 6455 opening-handshake checks, the frame reader and encoder, message reassembly, close codes and the
lingering close live in a horizontal library, planning name `Surl.WebSocket`. The WebSocket server keeps
serving byte for byte as today through that library. SignalR, SocketIo and Mqtt5's `mqtt5+ws` can then speak
WebSocket without referencing `Surl.Protocol.Ws`.

## Context

- Decision: BL-401's ADR, the WebSocket section: the library's name, which types move, its ADR-0002 row,
  and how a server names its subprotocol (`Sec-WebSocket-Protocol`). If its name differs from `Surl.WebSocket`,
  use the ADR's name and correct `touches`.
- Candidates, all in `Protocols/Http/Surl.Protocol.Ws.UnitLibrary`: `WebSocketAcceptKey`, `WebSocketUpgradeChecks`,
  `WebSocketUpgradeRefusal`, `WebSocketUpgradeResponder`, `WebSocketHttpStatuses`, `WebSocketFrame`,
  `WebSocketFrameReader`, `WebSocketFrameEncoder`, `WebSocketFrameReadOutcome`, `WebSocketFrameReadResult`,
  `WebSocketMessage`, `WebSocketMessageReassembler`, `WebSocketReassemblyOutcome`, `WebSocketReassemblyStep`,
  `WebSocketOpcode`, `WebSocketOpcodes`, `WebSocketCloseCodes`, `WebSocketLingeringClose`. Content serving
  (`WebSocketServedEntry`, `WebSocketExchange`, `WsProtocolServer`) stays in the server.
- The handshake rides `Surl.HttpMessage` (ADR-0070), so the new library references it and `Surl.Protocol.Abstractions`.
- ADR-0071 governs the WebSocket server's behaviour. Nothing here changes it.
- New projects go in `Protocols/Http/` beside `Surl.Protocol.Ws`, are listed in `Surl.slnx` under `/Protocols/Http/`,
  and the library's row joins `ProtocolIsolationTests`' `HorizontalLibraries`.

## Acceptance criteria

- [x] `Protocols/Http/Surl.WebSocket.UnitLibrary` and `Protocols/Http/Surl.WebSocket.UnitTests` exist and are in
      `Surl.slnx`. The moved types' tests moved with them.
- [x] Every remaining `Surl.Protocol.Ws.UnitTests` test passes, with no expected byte edited.
      `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~WebSocket"` passes on Windows.
- [x] A named test accepts an upgrade that asks for subprotocol `mqtt`, through the library's subprotocol
      choice. The `Sec-WebSocket-Protocol: mqtt` response field is written from RFC 6455 section 4.2.2.
- [x] `ProtocolIsolationTests` passes with the new row.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

- Followed ADR-0084 decision 2 where it differs from the candidate list above:
  `WebSocketUpgradeResponder` stays in `Surl.Protocol.Ws` (it owns ADR-0071's check order, the
  content store and the login). The other 17 candidates moved with `git mv`, names unchanged,
  namespace `Surl.WebSocket`.
- Every moved type is `public`: `Surl.Protocol.Ws` uses each of them (the exchange uses the codec
  and reassembly, the responder the checks, statuses and lingering close), which is ADR-0084's
  rule for `public`.
- New `WebSocketUpgradeAcceptance.Accept(request, serverSubprotocols, now)` builds the `101`
  (`Date`, `Server`, `Upgrade`, `Connection`, `Sec-WebSocket-Accept`, then
  `Sec-WebSocket-Protocol` when one is chosen) and reports `ChosenSubprotocol`. Default taken:
  subprotocol names are compared ordinally, because they are tokens and RFC 6455 section 4.2.2
  has the server echo one of the client's values exactly. The responder passes `[]`, so the
  WebSocket server's bytes are unchanged; no expected byte in `Surl.Protocol.Ws.UnitTests` was edited.
- Moved tests: the accept key, close codes, frame encoder and reader, and the reassembler (with
  `ReadCountingConnection`). `Rfc6455Examples` is copied, because `WsExchangeTests` uses it too;
  `ManualTimeProvider` is copied for the lingering-close tests. New tests in the library:
  `WebSocketUpgradeChecksTests`, `WebSocketLingeringCloseTests` and `WebSocketUpgradeAcceptanceTests`
  (`Accept_ClientAsksForMqtt_ChoosesMqttAndNamesItInSecWebSocketProtocol` is the `mqtt` criterion),
  so the library is covered by its own tests.
- Added `Documentation/Wiki/Glossary.md` to `touches`: ADR-0084's consequences put
  `WebSocketUpgradeAcceptance` in the glossary in this task, and no task in `Doing` touches it.
- Measured: Surl.WebSocket 100% line / 100% branch (54 members, worst CRAP 10);
  Surl.Protocol.Ws 100% / 100% (51 members, worst CRAP 9). Tests: Surl.WebSocket.UnitTests 109,
  Surl.Protocol.Ws.UnitTests 100, Conformance `~WebSocket` 27, all passing.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Surl.WebSocket holds RFC 6455's handshake checks, framing, reassembly, close codes and lingering close; WebSocketUpgradeAcceptance chooses a subprotocol; the WebSocket server serves byte for byte through it
