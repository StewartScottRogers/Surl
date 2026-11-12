---
id: BL-762
title: Serve Socket.IO's HTTP long-polling transport and its upgrade to WebSocket
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-454, BL-407, BL-1035]
touches: [Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests, Documentation/Conformance/Extended/SocketIo.md, Documentation/Planning/Decisions]
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

- 2026-11-11 (lane 1): cherry-picked b329047e from factory/BL-762-lane-3-20261111-060243 cleanly onto the current head, but `dotnet build` cannot run: global.json pins SDK 10.0.401 and only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed. Waiting on BL-1035. Next run: cherry-pick b329047e and 1ff773e0 (1ff773e0 also edits the ADR-0120 file under Documentation/Planning/Decisions, which is not in this task's touches; add it once BL-671 leaves Doing), then build and fix.

- 2026-11-11 (lane 4): cherry-picked b329047e and 1ff773e0 (SDK 10.0.404 now rolls forward, so the build runs). Added Documentation/Planning/Decisions to touches: 1ff773e0 records ADR-0120 amendment 2; no task in Doing names it. Build clean, 278 SocketIo tests green, library at 100% line and branch coverage.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Lane 3 could not integrate: build failed after rebasing onto the other lanes' work. The work is on branch factory/BL-762-lane-3-20261111-060243; start with git cherry-pick --no-commit factory/BL-762-lane-3-20261111-060243 and fix it.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waiting on BL-1035: the pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Socket.IO serves Engine.IO HTTP long-polling and its 2probe/3probe/5 upgrade to WebSocket
