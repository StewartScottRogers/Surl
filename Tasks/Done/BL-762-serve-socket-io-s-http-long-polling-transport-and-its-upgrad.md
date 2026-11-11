---
id: BL-762
title: Serve Socket.IO's HTTP long-polling transport and its upgrade to WebSocket
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-454, BL-407]
touches: [Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests, Documentation/Conformance/Extended/SocketIo.md, Documentation/Planning/Decisions/ADR-0120-how-the-socket-io-server-answers-socketio-and-socketios.md]
requirement: none
created: 2026-11-05
completed: 2026-11-11
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

- [x] Named tests show: the polling handshake answered `200` with the open packet whose `upgrades` is
      `["websocket"]`; a `POST` answered `200` `ok`; a held `GET` answered when a packet is queued and
      answered `6` (noop) on upgrade; packets joined with `\x1e` and binary sent `b`-prefixed base64; an
      unknown `sid` answered `400` code 1; a second concurrent `GET` or `POST` answered `400` code 3 and
      the session closed; and an upgrade `2probe` answered `3probe`, then `5` moving the session to the
      WebSocket.
- [x] Every `Documentation/Conformance/Extended/SocketIo.md` row marked "n/a: BL-758" names a test.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and
      branch coverage.

## Notes

- Plan: `SocketIoSession` became transport-free (Engine.IO packets in, a `SocketIoOutbox` of
  `SocketIoOutboxItem` packets out, the clock, its polls, posts and upgrade state in
  `SocketIoTransportState`). `SocketIoWebSocketTransport` carries it over a WebSocket (frames, the
  `2probe`/`3probe`/`5` probe, the end's Close and the limit farewell); `SocketIoPollingAnswerer`
  answers the polling handshake, held `GET`s and `POST`s (`SocketIoPollingBodyBuffer` bounds the body);
  `SocketIoAnswers` writes the HTTP answers. Copied `McpMessageBuffer`'s shape for the body buffer
  (code, not a verdict).
- Decisions recorded as ADR-0120 amendment 2 (Decided by Claude under Stewart's delegation): a polling
  session lives on no connection (heartbeat, connect timeout or shutdown end it; logins judged with the
  opening connection's `TlsSession`); the order of refusals with `polling` served; a poll after the end
  is `1`; a post past `maxPayload` or the upload limit is `413`, a malformed, cut-short or empty post is
  code 3, each closing the session; a failed probe ends that WebSocket only and leaves polling.
- Touches: added the ADR-0120 file, because the decisions above needed an amendment; no task in Doing
  names it.
- The acceptance criterion's "n/a: BL-758" means the matrix's "n/a: BL-762" rows (EIO-Polling-1,
  EIO-Polling-2, EIO-Upgrade-1), the ID in the task text being a typo; every one now names tests, and
  the matrix gains Surl-2-4, Surl-2-5, Surl-2-6 and Surl-7-6.
- Toolchain: the pinned SDK 10.0.401 is not installed on the lane machine (BL-1035). Build, fast tests
  and coverage were verified with `global.json` temporarily rolled forward to SDK 11.0.100 and restored
  afterwards: `dotnet build Surl.slnx -warnaserror` clean, fast tests green in all 79 test projects,
  `Surl.Extended.Protocol.SocketIo.UnitTests` 278 green (5 runs), library at 100% line and 100%
  branch coverage. CI on the pinned SDK still gates the merge to `master`.
- Added a `SocketIoPacketReaderTests` row (a type character below `0`) to close a branch the reader
  already had uncovered.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Socket.IO serves Engine.IO v4 HTTP long-polling (handshake, held GET, POST, 413 and code 1/3 refusals) and the 2probe/3probe/5 upgrade to WebSocket
