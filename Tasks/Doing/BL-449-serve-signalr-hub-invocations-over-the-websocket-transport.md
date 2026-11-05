---
id: BL-449
title: Serve SignalR hub invocations over the WebSocket transport
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-448, BL-408]
touches: [Protocols/Http/Surl.Extended.Protocol.SignalR.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SignalR.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-449 — Serve SignalR hub invocations over the WebSocket transport

## Goal

The SignalR server upgrades `<hub>?id=<connectionToken>` to WebSocket and completes the hub-protocol handshake.
It answers invocations with completions, stream invocations with stream items, and pings and closes. It sends
keep-alive pings, and closes a connection whose client went quiet past the timeout.

## Context

- Decision: BL-447's ADR, decisions 1, 3, 4, 6 and 7.
- The WebSocket handshake and frames: BL-408's library (add its reference).
- HubProtocol.md: "Overview" (handshake first, then messages), "Invocation", "StreamInvocation", "CancelInvocation",
  "Completion" (result or error), "Ping", "Close" (`error`, `allowReconnect`), and the non-blocking invocation
  (no `invocationId`, so no completion).
- Time through an injected `TimeProvider`. Tests use the test project's `ManualTimeProvider`.

## Acceptance criteria

- [ ] Named tests, through an in-memory connection with WebSocket frames written from RFC 6455 and hub messages
      from HubProtocol.md, show:
      - the handshake answered with `{}`;
      - an unsupported protocol answered with the handshake error;
      - an echo invocation completed with its result;
      - a non-blocking invocation getting no completion;
      - a failing method completed with `error`;
      - a stream invocation sending items then a completion;
      - a CancelInvocation stopping the stream;
      - a ping every keep-alive interval;
      - a quiet client closed with a `Close` message after the timeout;
      - a connection token not from negotiate refused.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
