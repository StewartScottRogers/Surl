---
id: BL-454
title: Serve Socket.IO sessions over the WebSocket transport
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-453, BL-408]
touches: [Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-454 — Serve Socket.IO sessions over the WebSocket transport

## Goal

The Socket.IO server accepts `GET /socket.io/?EIO=4&transport=websocket` and opens an Engine.IO session. It keeps
the session alive with pings, connects namespaces after checking `auth`, and answers events and acks as
BL-452's ADR decides. It closes on a missed pong or a DISCONNECT. HTTP long-polling is served too if the ADR
decides so.

## Context

- Decision: BL-452's ADR, decisions 1 to 7.
- The WebSocket handshake and frames: BL-408's library (add its reference). Long-polling, if served:
  BL-407's library.
- Engine.IO v4: "Handshake", "Heartbeat" (the server sends `2`, the client answers `3`; no pong within
  `pingTimeout` closes), and "Upgrade" if polling is served. Socket.IO v5: "Connection to a namespace",
  "Sending and receiving data", "Acknowledgement", "Disconnection from a namespace".
- Time through an injected `TimeProvider`.

## Acceptance criteria

- [ ] Named tests, with WebSocket frames from RFC 6455 and packets from the two specifications, show:
      - the open packet sent;
      - CONNECT to `/` with good `auth` answered with the `sid` payload;
      - bad `auth` gets CONNECT_ERROR;
      - an unknown namespace gets CONNECT_ERROR;
      - an event echoed;
      - an event with an ack id answered with ACK;
      - a binary event with its attachments;
      - a ping every `pingInterval`;
      - a missed pong closing the session;
      - an over-`maxPayload` packet closing it.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- 2026-11-05 (BL-452): ADR-0120 is the ADR. It serves HTTP long-polling, but in BL-762, not here: this task
  answers `transport=polling` with `400` code 0 and sends `upgrades` `[]`.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
