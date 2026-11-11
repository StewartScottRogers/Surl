---
id: BL-762
title: Serve Socket.IO's HTTP long-polling transport and its upgrade to WebSocket
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-454, BL-407]
touches: [Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests, Documentation/Conformance/Extended/SocketIo.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-762 — Serve Socket.IO's HTTP long-polling transport and its upgrade to WebSocket

## Goal

The Socket.IO server answers `GET` and `POST /socket.io/?EIO=4&transport=polling` as an Engine.IO v4 HTTP
long-polling session, and moves such a session to WebSocket with the `2probe`/`3probe`/`5` upgrade, as
ADR-0120 decision 2 decides.

## Context

- Decision: [ADR-0120](../../Documentation/Planning/Decisions/ADR-0120-how-the-socket-io-server-answers-socketio-and-socketios.md)
  decision 2 (the polling rules and their `400` codes) and decision 7 (limits). BL-454 serves the
  WebSocket transport first and answers `transport=polling` with `400` code 0 until this task lands.
- Specification: Engine.IO protocol v4, github.com/socketio/engine.io-protocol `main` at
  `f21de7b00ed09b3bbad2807db718ea5d6bc36aba`: "HTTP long-polling" (request path, query parameters,
  headers, sending and receiving data), "Upgrade", and "Packet encoding" ("HTTP long-polling": the `\x1e`
  separator and `b`-prefixed base64 binary).
- HTTP: `Surl.HttpServing` (BL-407), already referenced by BL-454 for the upgrade.
- Time through the injected `TimeProvider`.

## Acceptance criteria

- [ ] Named tests show: the polling handshake answered `200` with the open packet whose `upgrades` is
      `["websocket"]`; a `POST` answered `200` `ok`; a held `GET` answered when a packet is queued and
      answered `6` (noop) on upgrade; packets joined with `\x1e` and binary sent `b`-prefixed base64; an
      unknown `sid` answered `400` code 1; a second concurrent `GET` or `POST` answered `400` code 3 and
      the session closed; and an upgrade `2probe` answered `3probe`, then `5` moving the session to the
      WebSocket.
- [ ] Every `Documentation/Conformance/Extended/SocketIo.md` row marked "n/a: BL-758" names a test.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and
      branch coverage.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
