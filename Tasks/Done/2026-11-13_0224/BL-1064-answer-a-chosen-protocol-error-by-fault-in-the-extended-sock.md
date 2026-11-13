---
id: BL-1064
title: Answer a chosen protocol error by fault in the extended Socket.IO server
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-823, BL-785]
touches: [Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.SocketIo.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md]
requirement: FR-062
created: 2026-11-12
completed: 2026-11-13
---
# BL-1064 — Answer a chosen protocol error by fault in the extended Socket.IO server

## Goal

An `error` fault rule naming `socketio` or `socketios` (or a listener of one) makes the Socket.IO server answer the matching request with the chosen error, instead of being refused at configuration.

## Context

- Decision: [ADR-0124](../../Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md) decisions 4 and 9. Reply shape: text sent as a Socket.IO `CONNECT_ERROR` packet `message` for a namespace connect, or an error acknowledgement for an event (`command` the event name, `path` the namespace).
- Filed by BL-837's audit: the scheme is registered by `Surl.Composition` but has no row in `FaultReplyShapes` (`Surl.Cli`).
- Consult `ExchangeContext.Faults` per request after login and publish `FaultAppliedEvent` (BL-823's contracts and doubles), as BL-835 did for CoAP, MCP and SignalR.
- Add this family's row to ADR-0124's reply table and its shape to `FaultReplyShapes` (BL-785's `--fault` reply check).

## Acceptance criteria

- [x] Named tests: a request matching an `error` rule gets exactly the chosen error, a non-matching one is served, and one `FaultAppliedEvent` is published.
- [x] Named `Surl.Cli` tests: one accepted and one refused `reply` per scheme.
- [x] ADR-0124's reply table has this family's row.
- [x] With no rule, every existing test of the touched libraries passes unchanged.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the touched libraries keep 100% line and branch coverage.

## Notes

- Decided under delegation (ADR-0124 row added): reply is any non-empty text. CONNECT consulted after login accepted (command `connect`, path the namespace, account the login user name); events by event name. Error ack is `[{"error":"text"}]` as a plain ACK; an event with no ack id is swallowed since nothing can carry the error. Drop ends the session silently (a session has no connection of its own); delay holds. Coverage of the three touched classes is 100% line and branch; build -warnaserror clean; SocketIo (288) and Cli (1858) tests green.

## Log

- 2026-11-12: Created.
- 2026-11-13: Backlog -> Doing.
- 2026-11-13: Doing -> Done. An error fault rule on socketio/socketios answers a CONNECT with a CONNECT_ERROR or an acked event with an error ACK
