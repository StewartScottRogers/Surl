---
id: BL-408
title: Create Surl.WebSocket and move the WebSocket handshake and framing out of Surl.Protocol.Ws
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-401, BL-404]
touches: [Protocols/Http/Surl.WebSocket.UnitLibrary, Protocols/Http/Surl.WebSocket.UnitTests, Protocols/Http/Surl.Protocol.Ws.UnitLibrary, Protocols/Http/Surl.Protocol.Ws.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed:
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

- [ ] `Protocols/Http/Surl.WebSocket.UnitLibrary` and `Protocols/Http/Surl.WebSocket.UnitTests` exist and are in
      `Surl.slnx`. The moved types' tests moved with them.
- [ ] Every remaining `Surl.Protocol.Ws.UnitTests` test passes, with no expected byte edited.
      `dotnet test Conformance/Surl.Conformance.UnitTests --filter "FullyQualifiedName~WebSocket"` passes on Windows.
- [ ] A named test accepts an upgrade that asks for subprotocol `mqtt`, through the library's subprotocol
      choice. The `Sec-WebSocket-Protocol: mqtt` response field is written from RFC 6455 section 4.2.2.
- [ ] `ProtocolIsolationTests` passes with the new row.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
