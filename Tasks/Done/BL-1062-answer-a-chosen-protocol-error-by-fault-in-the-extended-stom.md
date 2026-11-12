---
id: BL-1062
title: Answer a chosen protocol error by fault in the extended STOMP broker
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-823, BL-785]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Stomp.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md]
requirement: FR-062
created: 2026-11-12
completed: 2026-11-12
---
# BL-1062 — Answer a chosen protocol error by fault in the extended STOMP broker

## Goal

An `error` fault rule naming `stomp` or `stomps` (or a listener of one) makes the STOMP broker answer the matching request with the chosen error, instead of being refused at configuration.

## Context

- Decision: [ADR-0124](../../Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md) decisions 4 and 9. Reply shape: text sent as a STOMP `ERROR` frame `message` header, after which the connection closes as STOMP 1.2 says (`command` the client frame, `path` the destination).
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

- Decided by Claude under Stewart's delegation: the reply is one line of text (no CR, LF or NUL; the CLI already refuses control characters), sent as ERROR `message`; a drop ends the connection after queued frames are written; faults apply to every frame after login, `path` being the `destination` header. Recorded in ADR-0124's reply table.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. STOMP broker answers a chosen error by fault
