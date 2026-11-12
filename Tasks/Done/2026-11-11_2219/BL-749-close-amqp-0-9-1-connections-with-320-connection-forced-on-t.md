---
id: BL-749
title: Close AMQP 0-9-1 connections with 320 CONNECTION_FORCED on the idle timeout, max-time and shutdown
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-505, BL-1035]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Amqp0.UnitTests, Documentation/Conformance/Extended/Amqp0.md]
requirement: none
created: 2026-11-05
completed: 2026-11-11
---
# BL-749 — Close AMQP 0-9-1 connections with 320 CONNECTION_FORCED on the idle timeout, max-time and shutdown

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

- [x] Named tests show `connection.close` 320 with the decided fixed text, class 0 and method 0,
      written after `open-ok` when the exchange is cancelled for a limit, and for shutdown.
- [x] Before `open-ok`, a cancelled exchange still writes no bytes.
- [x] Rows `Amqp0:Surl-Idle-1` and `Amqp0:Surl-Limits-2` of `Amqp0.md` name those tests.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100%
      line and branch coverage.

## Notes

- 2026-11-11 (lane 2): Parked before any code. `dotnet build` fails: global.json pins SDK 10.0.401 and only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed. Depends on BL-1035 (Stewart reinstalls the SDK).

- 2026-11-11 (lane 6): Decided one fixed text, `CONNECTION_FORCED - idle timeout, maximum time or shutdown`, because the context does not say which limit fired; ADR-0112 decision 3 amended. Any cancellation after open-ok (limit or shutdown) writes connection.close 320 (class 0, method 0) on a 1-second `FarewellWriteDeadline` of its own, then rethrows; skipped if a close was already sent. Idle-timer vs broker heartbeats: the idle timer is the engine's, not changed here.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so dotnet build cannot run
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Done. Open AMQP 0-9-1 connections cancelled for a limit or shutdown get connection.close 320 before closing
