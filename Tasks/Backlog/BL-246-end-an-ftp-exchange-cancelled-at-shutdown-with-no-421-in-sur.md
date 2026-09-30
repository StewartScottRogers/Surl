---
id: BL-246
title: End an FTP exchange cancelled at shutdown with no 421 in Surl.Protocol.Ftp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-234]
touches: [Surl.Protocol.Ftp.UnitLibrary, Surl.Protocol.Ftp.UnitTests]
requirement: FR-038
created: 2026-09-30
completed:
---
# BL-246 — End an FTP exchange cancelled at shutdown with no 421 in Surl.Protocol.Ftp

## Goal

When the engine cancels an FTP exchange at shutdown, `FtpProtocolServer` ends it with no
`421 Timeout, closing`, and it still answers the idle timeout and maximum duration with that
`421`, as ADR-0059 decides.

## Context

- BL-230 made `FtpProtocolServer.ServeAsync` answer every cancellation of the exchange with
  `421 Timeout, closing`, shutdown included, because a server could not tell them apart.
- BL-234 (ADR-0059) added `ExchangeContext.ShutdownToken` and `IsCancelledForALimit`, and
  `SmtpSession` shows the pattern: catch the cancellation only `when (context.IsCancelledForALimit)`,
  and link the limit reply's one-second deadline to `ShutdownToken`, never to the exchange token.
- FTP's `WriteLimitReplyAsync` today runs on its own deadline alone; ADR-0059 decision 3 links
  it to `ShutdownToken` so shutdown still cuts a limit reply off.

## Acceptance criteria

- [ ] A fast test in `Surl.Protocol.Ftp.UnitTests` with both tokens cancelled (shutdown) shows
      `ServeAsync` throws `OperationCanceledException` and writes nothing after the last reply.
- [ ] `FtpLimitTests.ExchangeCancelledWhileWaitingForACommand_Answers421TimeoutClosingAndCloses`
      still passes.
- [ ] `Surl.Protocol.Ftp.UnitLibrary/CLAUDE.md` and the `FtpProtocolServer` remarks say
      shutdown ends with no farewell.
- [ ] `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Ftp.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
