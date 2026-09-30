---
id: BL-228
title: Answer the FTP idle timeout and maximum duration with 421 in Surl.Protocol.Ftp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-177]
touches: [Surl.Protocol.Ftp.UnitLibrary, Surl.Protocol.Ftp.UnitTests]
requirement: FR-038
created: 2026-09-29
completed:
---
# BL-228 — Answer the FTP idle timeout and maximum duration with 421 in Surl.Protocol.Ftp

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

- [ ] A fast test on a fake `TimeProvider` cancels the exchange token while the server waits for
      a command and asserts the server wrote `421 Timeout, closing\r\n`, completed writes and did
      not abort.
- [ ] A reply the peer does not take within the deadline closes the connection without aborting.
- [ ] `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ftp.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

- Filed by BL-177, which left this case out: its acceptance criteria name only the head timeout.

## Log

- 2026-09-29: Created.
