---
id: BL-1063
title: Answer a chosen protocol error by fault in the extended XMPP server
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-823, BL-785]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests, Application/Surl.Cli.UnitLibrary, Application/Surl.Cli.UnitTests, Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md]
requirement: FR-062
created: 2026-11-12
completed: 2026-11-13
---
# BL-1063 — Answer a chosen protocol error by fault in the extended XMPP server

## Goal

An `error` fault rule naming `xmpp` or `xmpps` (or a listener of one) makes the XMPP server answer the matching request with the chosen error, instead of being refused at configuration.

## Context

- Decision: [ADR-0124](../../Documentation/Planning/Decisions/ADR-0124-surl-is-a-stand-in-for-tests.md) decisions 4 and 9. Reply shape: a stanza-error condition of RFC 6120 section 8.3.3 (for example `service-unavailable`) sent as `<error type=...>` on the matching stanza (`command` the stanza kind, `path` the addressed JID).
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

- Decided by Claude under Stewart's delegation: the reply is a bare RFC 6120 section 8.3.3 condition name; the error type comes from section 8.3.2's table. `command` is the stanza kind, `path` its `to`, account the localpart. Drop rules abort the connection, so the router now takes the `IConnection`. Recorded as a row in ADR-0124's reply table, no new ADR.
- `Surl.Extended.Protocol.Grpc.UnitTests` `ServeStreamAsync_ShutdownBeforeAnyReply_IsTrailersOnlyUnavailable` is flaky (failed once in four runs, untouched by this task).

## Log

- 2026-11-12: Created.
- 2026-11-13: Backlog -> Doing.
- 2026-11-13: Doing -> Done. Xmpp error faults answer a chosen stanza error condition
