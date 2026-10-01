---
id: BL-247
title: Answer the IMAP idle timeout and maximum duration with BYE in Surl.Protocol.Imap
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-234]
touches: [Surl.Protocol.Imap.UnitLibrary, Surl.Protocol.Imap.UnitTests]
requirement: FR-044
created: 2026-09-30
completed: 2026-09-30
---
# BL-247 — Answer the IMAP idle timeout and maximum duration with BYE in Surl.Protocol.Imap

## Goal

When an IMAP exchange is cancelled for the idle timeout or the maximum exchange duration,
`Surl.Protocol.Imap`'s server writes `* BYE surl Timeout, closing` and closes, as ADR-0055's
limits table and ADR-0006 section 5's IMAP column say; at shutdown it ends with no `BYE`.

## Context

- BL-234 (ADR-0059) added `ExchangeContext.ShutdownToken` and `IsCancelledForALimit`.
  `Surl.Protocol.Smtp`'s `SmtpSession.RunAsync` shows the pattern: catch the exchange's
  cancellation only `when (context.IsCancelledForALimit)`, write the farewell within the
  one-second limit-reply deadline linked to `ShutdownToken`, never to the exchange token, then
  complete writes.
- curl answers `* BYE` to a fetch with exit 78 (ADR-0055 row 22).

## Acceptance criteria

- [x] A fast test in `Surl.Protocol.Imap.UnitTests` on a fake `TimeProvider` shows the idle
      timeout answered `* BYE surl Timeout, closing`, then writes completed; another shows the
      maximum exchange duration answered the same.
- [x] A fast test shows shutdown (both tokens cancelled) ends the exchange with no `BYE`.
- [x] `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Imap.UnitLibrary`.

## Notes

- Done directly rather than through the full `/feature` stages: the change is SMTP's BL-234
  pattern (ADR-0059) applied to `ImapSession.RunAsync`, and ADR-0055's limits table already fixes
  the bytes (`* BYE surl Timeout, closing`), so no new decision or measurement was needed.
- `CloseWithAsync`, which writes every IMAP limit response, now links its one-second deadline to
  `ShutdownToken` instead of the exchange token, as SMTP's `WriteLimitReplyAsync` does: a limit's
  cancellation may already have cancelled the exchange token. So a head-timeout or line-too-long
  response whose write stalls when the idle timeout fires is now closed at the deadline, not thrown;
  the old tests that cancelled the exchange token alone now cancel shutdown too.
- Tests: `ImapLimitTests.ServeAsync_IdleTimeout_SaysByeTimeoutClosingThenCompletesWrites`,
  `ServeAsync_MaxExchangeDurationInsideAnAppend_SaysByeTimeoutClosingStoresNothingAndCompletesWrites`,
  `ServeAsync_Shutdown_EndsWithNoBye`, `ServeAsync_ShutdownInsideALiteral_Throws`,
  `ServeAsync_ShutdownWhileALimitResponseWaits_Throws`,
  `ServeAsync_ExchangeCancelledForALimitWhileALimitResponseWaits_StillClosesAtTheWriteDeadline`.
- `Measure-CodeQuality.ps1 -Library Surl.Protocol.Imap.UnitLibrary`: 100% line, 100% branch,
  0 failing members.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. IMAP answers the idle timeout and maximum exchange duration with * BYE surl Timeout, closing; shutdown ends with no BYE
