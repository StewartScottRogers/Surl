---
id: BL-743
title: Close AMQP 0-9-1 connections with 320 CONNECTION_FORCED on the idle timeout, max-time and shutdown
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-505]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests, Documentation/Conformance/Extended/Amqp0.md]
requirement: none
created: 2026-11-05
completed:
---
# BL-743 — Close AMQP 0-9-1 connections with 320 CONNECTION_FORCED on the idle timeout, max-time and shutdown

## Goal

Once `connection.open-ok` was sent, an AMQP 0-9-1 connection the engine ends for `--idle-timeout`,
`--max-time` or the server stopping is told so with `connection.close` 320 `CONNECTION_FORCED`
before it closes, as ADR-0112 decisions 3 and 7 and rows `Amqp0:Surl-Idle-1` and
`Amqp0:Surl-Limits-2` of `Documentation/Conformance/Extended/Amqp0.md` require.

## Context

- BL-505 built the connection class (`Amqp0BrokerConnection` in
  `Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary`). When `ExchangeContext.CancellationToken`
  is cancelled after `open-ok`, the read throws `OperationCanceledException` and the exchange ends
  with no `connection.close`.
- `ExchangeContext.IsCancelledForALimit` tells a limit from shutdown; `ShutdownToken` tells
  shutdown. A server so cancelled may write its farewell on a short deadline of its own, never on
  `CancellationToken` (ADR-0006 section 5; ADR-0059). The context does not say *which* limit fired,
  so ADR-0112's fixed text `CONNECTION_FORCED - idle timeout` cannot be told from max-time: decide
  the text (one shared fixed phrase, or a context property) and amend ADR-0112 if it changes.
- Before `open-ok` every limit closes with no bytes (already the case).
- Whether the engine's idle timer counts the broker's own heartbeats as activity decides whether
  `--idle-timeout` can fire on a heartbeating connection; check `Surl.Core`'s `ExchangeDeadlines`.

## Acceptance criteria

- [ ] Named tests show `connection.close` 320 with the decided fixed text, class 0 and method 0,
      written after `open-ok` when the exchange is cancelled for a limit, and for shutdown.
- [ ] Before `open-ok`, a cancelled exchange still writes no bytes.
- [ ] Rows `Amqp0:Surl-Idle-1` and `Amqp0:Surl-Limits-2` of `Amqp0.md` name those tests.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-05: Created.
