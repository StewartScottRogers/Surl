---
id: BL-755
title: Serve SignalR stateful reconnect with Ack and Sequence messages
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-449]
touches: [Protocols/Http/Surl.Extended.Protocol.SignalR.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SignalR.UnitTests, Documentation/Conformance/Extended/SignalR.md]
requirement: none
created: 2026-11-05
completed:
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

- [ ] Named tests, on a `ManualTimeProvider`, show: the negotiate offer for each query name, and none without it;
      Acks sent for received messages; a reconnect within 30 s answered `{}` then Sequence and the unacknowledged
      messages replayed in order; duplicates below a client Sequence dropped; a reconnect after 30 s `404`; a reconnect
      by another caller `403`; the 100000-byte bound ending the connection.
- [ ] The matrix rows are replaced; `dotnet build -warnaserror` is clean, the fast tests are green, and the library
      keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
