---
id: BL-230
title: Answer the FTP idle timeout and maximum duration with 421 in Surl.Protocol.Ftp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-177]
touches: [Surl.Protocol.Ftp.UnitLibrary, Surl.Protocol.Ftp.UnitTests]
requirement: FR-038
created: 2026-09-29
completed: 2026-09-30
---
# BL-230 — Answer the FTP idle timeout and maximum duration with 421 in Surl.Protocol.Ftp

## Goal

When `Surl.Core` ends an FTP exchange for its idle timeout or maximum duration, by cancelling
`ExchangeContext.CancellationToken`, `FtpProtocolServer` first writes `421 Timeout, closing` and
completes writes within a short deadline, as ADR-0052 decision 10 and ADR-0006 section 5's FTP
column say.

## Context

- BL-177 built the control connection; today a cancelled exchange token propagates
  `OperationCanceledException` out of `ServeAsync` with no reply
  (`FtpLimitTests.WaitBetweenCommands_IsNotCutOffByTheHeadTimeout` shows it).
- ADR-0006 section 5: "Idle timeout, maximum duration" -> FTP `421`, then close. ADR-0052
  decision 10: `421 Timeout, closing`; measured, curl 8.21.0 exits 28 on it (row 47).
- Check first how `Surl.Core`'s `ServingEngine` cancels an exchange and whether it still lets the
  server write after the token is cancelled (no other protocol server writes on idle yet: IMAP's
  `* BYE` in the same ADR-0006 row is the other case). If the engine gives no room to write, that
  is a `Surl.Core` change: file it and depend on it.
- The write must use its own deadline (as `FtpProtocolServer.LimitReplyWriteDeadline` does), not
  the cancelled exchange token.

## Acceptance criteria

- [x] A fast test on a fake `TimeProvider` cancels the exchange token while the server waits for
      a command and asserts the server wrote `421 Timeout, closing\r\n`, completed writes and did
      not abort.
- [x] A reply the peer does not take within the deadline closes the connection without aborting.
- [x] `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ftp.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

- Filed by BL-177, which left this case out: its acceptance criteria name only the head timeout.
- Checked first: `ServingEngine` awaits `ServeAsync` to its end after cancelling the exchange token and
  lets the server keep writing to the connection, so no `Surl.Core` change was needed.
- `ServeAsync` catches an `OperationCanceledException` from the command loop only when the exchange's
  token is cancelled, disposes the responder (closing any data connection) first, then writes
  `421 Timeout, closing` and completes writes. A cancellation thrown while the exchange is not
  cancelled still escapes (`CancellationThrownWhileTheExchangeIsNotCancelled_PropagatesWithoutAReply`).
- Choice: `WriteLimitReplyAsync` now runs on its own one-second deadline alone, no longer linked to the
  exchange token. That is what lets the cancellation reply be written at all, and a head-timeout or
  line-too-long reply already being written when the exchange is cancelled now gets its second and
  closes instead of throwing (`ExchangeCancelledWhileTheLimitReplyIsWritten_StillClosesAtTheWriteDeadline`
  replaces the test that pinned the throw). The engine's shutdown also cancels the exchange token, so
  a shutdown gives up to one second for the same reply; no new ADR, since ADR-0052 decision 10 already
  pins the reply and curl's exit 28 (row 47).
- Tests: `FtpLimitTests.ExchangeCancelledWhileWaitingForACommand_Answers421TimeoutClosingAndCloses`,
  `CancellationReplyNotTakenWithinTheWriteDeadline_ClosesWithoutAborting`. Measure-CodeQuality: FTP
  library 100% line, 100% branch, 195 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. FTP answers the idle timeout and maximum duration with 421 Timeout, closing, then closes
