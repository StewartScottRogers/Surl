---
id: BL-452
title: Decide how the Socket.IO server answers socketio and socketios
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-401]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/SocketIo.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-452 — Decide how the Socket.IO server answers socketio and socketios

## Goal

An ADR decides how `surl socketio://` and `socketios://` answer as an Engine.IO v4 and Socket.IO v5 server. It
covers the path, the transports, the handshake, the heartbeat, namespaces and their authentication, what the
server does with events and acknowledgements, binary attachments and limits. A server-side requirement matrix
traces each decision.

## Context

- Oracle and rules: BL-543's ADR. Specifications:
  - the Engine.IO protocol v4 (github.com/socketio/engine.io-protocol, v4 branch): the open packet
    `0{"sid":…,"upgrades":[…],"pingInterval":…,"pingTimeout":…,"maxPayload":…}`, packet types 0 to 6, the
    server-initiated ping in v4, and the polling and WebSocket transports;
  - the Socket.IO protocol v5 (github.com/socketio/socket.io-protocol, v5 branch): packet types CONNECT 0,
    DISCONNECT 1, EVENT 2, ACK 3, CONNECT_ERROR 4, BINARY_EVENT 5, BINARY_ACK 6, namespaces, the `auth` payload,
    and the ack id;
  - RFC 6455.
  Cite the commit of each branch read.
- Which requests a client sends (what to accept, not a verdict): the port's
  `Curl.Extended.Protocol.SocketIo.UnitLibrary` (the port's ADR-1035) and its matrix `SocketIo.md`. The port's
  reference server in `Curl.Conformance.Extended.UnitLibrary/SocketIo/` is copyable code.
- WebSocket: BL-408's library. HTTP long-polling, if served: BL-407's library.

Decide and record:

1. The path (`/socket.io/`), `EIO=4` checking, and the answer to another version.
2. The transports: WebSocket direct, and whether HTTP long-polling and the upgrade are served.
3. The open packet's values, and the heartbeat on an injected `TimeProvider`.
4. Namespaces served, CONNECT `auth` checked against the accounts file (secure by default; anonymous only under
   `--allow-anonymous`), and the CONNECT_ERROR payload.
5. What the server does with an EVENT (planning assumption: echo it back to the sender, and to the namespace
   for a `broadcast` event) and with an ack id (ACK with the same arguments).
6. Binary attachments: the placeholder `{"_placeholder":true,"num":N}` and the attachments that follow.
7. Limits: `maxPayload`, connections, and each ADR-0006 limit.

## Acceptance criteria

- [x] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 7, each citing its specification section,
      and is listed in the folder's `README.md`.
- [x] `Documentation/Conformance/Extended/SocketIo.md` exists in the matrix format, each "covered by" naming
      BL-453 or BL-454, or "n/a: <reason>".

## Notes

- ADR-0120 decides it. Neither specification has a `v4`/`v5` branch for its current version: the Engine.IO v4
  text is engine.io-protocol `main` `f21de7b0` and the Socket.IO v5 text socket.io-protocol `main` `4f633dd6`
  (their `v2`/`v3` and `v3`/`v4` branches hold older revisions); the ADR cites both commits in full.
- HTTP long-polling and its upgrade are served, as a complete Engine.IO server serves both transports; filed
  as BL-758. WebSocket lands first in BL-454, which answers `transport=polling` with `400` code 0 until then.
- The login is the CONNECT payload (`token`, or `username` and `password`) judged by `IAuthenticationPolicy`,
  not the HTTP requests as in ADR-0119's SignalR: SIO puts credentials there and a browser's WebSocket cannot
  set `Authorization`.
- No `Documentation/Product/Requirements.md` row was added: it is outside this task's `touches`, and BL-447's
  ADR-0119 set the same precedent.
- Written in-session rather than through `align-and-document`: the decisions needed the specifications read
  here, and the agent would only have received them second-hand.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. ADR-0120 decides how the Socket.IO server answers socketio and socketios; SocketIo.md traces each server requirement to BL-453, BL-454 or BL-758
