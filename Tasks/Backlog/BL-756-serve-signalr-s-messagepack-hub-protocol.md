---
id: BL-756
title: Serve SignalR's MessagePack hub protocol
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-449, BL-1035]
touches: [Protocols/Http/Surl.Extended.Protocol.SignalR.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SignalR.UnitTests, Documentation/Conformance/Extended/SignalR.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-756 — Serve SignalR's MessagePack hub protocol

## Goal

A SignalR client whose handshake asks for `{"protocol":"messagepack","version":1}` is answered `{}` and served the
same hub methods in MessagePack-encoded binary messages.

## Context

- Decision: ADR-0119 decision 3 (filed by BL-447). Specification: dotnet/aspnetcore `main` commit
  `6aacb16a27a677d16278a9814ed04f871020854b`, `HubProtocol.md` "MessagePack (MsgPack) encoding" (each message as an
  array, the VarInt length prefix of "Binary encoding", headers) and "Type Mappings".
- MessagePack is hand-written (no package): only the subset the hub needs, read and written over the BCL. The hub's
  methods (ADR-0119 decision 1) pass arguments through unchanged, so a MessagePack value is carried as its raw bytes.
- Matrix row `SignalR:HUB-MessagePack-1` becomes rows of their own naming tests.

## Acceptance criteria

- [ ] Named tests show each message type 1 to 7 round-tripped against the MessagePack examples in HubProtocol.md, a
      VarInt-prefixed message split across WebSocket messages reassembled, two in one message read separately, and
      `Echo`, `Count` and `Fail` answered in MessagePack.
- [ ] The handshake of `messagepack` version 1 is answered `{}`; the matrix row is replaced; `dotnet build -warnaserror`
      is clean, the fast tests are green, and the library keeps 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 2): Not started. `dotnet build` fails with "A compatible .NET SDK was not found": global.json pins 10.0.401 (latestFeature) and the machine has only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100. Added BL-1035 (Stewart reinstalls the SDK) to depends-on; the board restarts this task once it is Done.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035 - pinned .NET SDK 10.0.401 is not installed, so nothing builds
