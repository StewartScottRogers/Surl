---
id: BL-755
title: Serve SignalR stateful reconnect with Ack and Sequence messages
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-449, BL-1035]
touches: [Protocols/Http/Surl.Extended.Protocol.SignalR.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SignalR.UnitTests, Documentation/Conformance/Extended/SignalR.md]
requirement: none
created: 2026-11-05
completed: 2026-11-11
---
# BL-755 — Serve SignalR stateful reconnect with Ack and Sequence messages

## Goal

A SignalR client that negotiates with `useStatefulReconnect=true` (or `useAck=true`) can lose its WebSocket and
resume the same connection within 30 s, with no message lost or repeated to the hub.

## Context

- Decision: ADR-0119 decision 6 (filed by BL-447): the offer in negotiate, numbering of Invocation, StreamItem and
  Completion, Ack within 1 s, the 30 s reconnect window on the `TimeProvider`, the handshake then Sequence on resume,
  the 100000-byte unacknowledged bound.
- Specification: dotnet/aspnetcore `main` commit `6aacb16a27a677d16278a9814ed04f871020854b`, `HubProtocol.md`
  "Ack Message Encoding" and "Sequence Message Encoding", `TransportProtocols.md` "useAck".
- Matrix rows `SignalR:TP-Negotiate-8` and `SignalR:HUB-Ack-1` become rows of their own naming tests.

## Acceptance criteria

- [x] Named tests, on a `ManualTimeProvider`, show: the negotiate offer for each query name, and none without it;
      Acks sent for received messages; a reconnect within 30 s answered `{}` then Sequence and the unacknowledged
      messages replayed in order; duplicates below a client Sequence dropped; a reconnect after 30 s `404`; a reconnect
      by another caller `403`; the 100000-byte bound ending the connection.
- [x] The matrix rows are replaced; `dotnet build -warnaserror` is clean, the fast tests are green, and the library
      keeps 100% line and branch coverage.

## Notes

- 2026-11-11 (lane 2): parked behind BL-1035. `dotnet build` fails with "A compatible .NET SDK was not found": global.json pins 10.0.401 and only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed, so no test can be built or run. Work starts again once BL-1035 is Done.

- 2026-11-11 (lane 2): BL-1035 cleared (SDK 10.0.404 rolls forward), so the work was done. Built: HubAck/HubSequence (types 8/9), SignalRReconnectState (numbering, 100000-byte bound, duplicate drop), Suspend/ReconnectWindow in SignalRHubConnections, the negotiate offer, and the session/takeover wiring. Choices: the Ack is sent immediately after each numbered client message (meets the 1 s bound); Count streams in progress are not resumed on reconnect; only a Silent end (no Close either way) suspends. ADR-0119 decision 6 notes it. Measure-CodeQuality for the library: 0 failing members. Fast tests green.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so nothing builds
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. SignalR stateful reconnect: negotiate offer, Ack/Sequence numbering, 30 s resume with replay, 100000-byte bound
