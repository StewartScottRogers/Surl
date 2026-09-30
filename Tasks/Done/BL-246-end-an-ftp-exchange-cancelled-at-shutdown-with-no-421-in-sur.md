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
completed: 2026-09-30
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

- [x] A fast test in `Surl.Protocol.Ftp.UnitTests` with both tokens cancelled (shutdown) shows
      `ServeAsync` throws `OperationCanceledException` and writes nothing after the last reply.
- [x] `FtpLimitTests.ExchangeCancelledWhileWaitingForACommand_Answers421TimeoutClosingAndCloses`
      still passes.
- [x] `Surl.Protocol.Ftp.UnitLibrary/CLAUDE.md` and the `FtpProtocolServer` remarks say
      shutdown ends with no farewell.
- [x] `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Ftp.UnitLibrary`.

## Notes

- Followed SmtpSession's pattern (BL-234): ServeAsync answers 421 Timeout, closing only when ExchangeContext.IsCancelledForALimit; at shutdown the captured OperationCanceledException is rethrown with no farewell. WriteLimitReplyAsync links its one-second deadline to ShutdownToken and swallows only a deadline cancellation, so shutdown cuts a limit reply off and propagates. The log note for a limit cancellation is unchanged so FtpLimitTests.ExchangeCancelledWhileWaitingForACommand_Answers421TimeoutClosingAndCloses passes as written. New tests: FtpLimitTests.ExchangeCancelledAtShutdownWhileWaitingForACommand_ThrowsWithNoFarewell and ShutdownWhileALimitReplyIsWritten_Throws; FtpTestExchange.Context gained a shutdownToken parameter. No new ADR: ADR-0059 already decides this. Measure-CodeQuality: Surl.Protocol.Ftp.UnitLibrary 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. FTP ends a shutdown-cancelled exchange with no 421; limits still answer 421 Timeout, closing
