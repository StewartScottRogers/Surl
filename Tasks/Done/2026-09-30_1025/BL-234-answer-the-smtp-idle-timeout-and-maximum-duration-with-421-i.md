---
id: BL-234
title: Answer the SMTP idle timeout and maximum duration with 421 in Surl.Protocol.Smtp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-198]
touches: [Surl.Protocol.Smtp.UnitLibrary, Surl.Protocol.Smtp.UnitTests, Surl.Protocol.Abstractions.UnitLibrary, Surl.Protocol.Abstractions.UnitTests, Surl.Core.UnitLibrary, Surl.Core.UnitTests, Documentation/Planning/Decisions]
requirement: FR-043
created: 2026-09-29
completed: 2026-09-30
---
# BL-234 — Answer the SMTP idle timeout and maximum duration with 421 in Surl.Protocol.Smtp

## Goal

When an SMTP exchange is cancelled for the idle timeout or the maximum exchange duration,
`SmtpProtocolServer` writes `421 4.4.2 surl Timeout, closing` and closes, as ADR-0053 decision 7
and ADR-0006 section 5's FTP, SMTP column say.

## Context

- BL-198 built the SMTP server. Today the engine's idle and duration deadlines
  (`Surl.Core`'s `ExchangeDeadlines`, `ExchangeCancellation`) cancel
  `ExchangeContext.CancellationToken`, and the server ends with no bytes, because nothing tells
  a server why its exchange was cancelled or gives it a moment to say goodbye.
- Decide, in an ADR marked "Decided by Claude under Stewart's delegation", how a server learns
  the reason and gets a short write window for its farewell, so FTP (BL-177's `421`) and IMAP
  (`* BYE`) can use it too. A contract change in `Surl.Protocol.Abstractions` runs apart from
  every protocol task, which is the intent; split the contract and the SMTP use into two tasks
  if it is large.
- curl answers a `421` to `MAIL` with exit 55 (ADR-0053 row 17).

## Acceptance criteria

- [x] A fast test in `Surl.Protocol.Smtp.UnitTests` on a fake `TimeProvider` shows the idle
      timeout answered `421 4.4.2 surl Timeout, closing`, then writes completed; another shows
      the maximum exchange duration answered the same.
- [x] Shutdown still ends an SMTP exchange with no bytes.
- [x] `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for
      every library the task changes.

## Notes

- Decision (ADR-0059, decided by Claude under Stewart's delegation): `ExchangeContext` gains
  `ShutdownToken` (init, `None` by default; `ServingEngine` sets it from the new
  `ExchangeDeadlines.ShutdownToken`) and `IsCancelledForALimit` (exchange token cancelled,
  shutdown not). A token rather than a public reason enum: every server already handles tokens,
  and it answers the one question a server has. Small enough to land with the SMTP use in one task.
- `SmtpSession.RunAsync` catches the cancellation only `when (context.IsCancelledForALimit)`,
  notes "The exchange was cancelled for a limit; answered 421 and closed." and writes
  `421 4.4.2 surl Timeout, closing` through `WriteLimitReplyAsync`, whose one-second deadline is now
  linked to `ShutdownToken` instead of the exchange token - so shutdown still cuts any limit reply
  off and throws, as before, while a limit's cancellation no longer does.
- Touches widened (no task in Doing named them): `Surl.Protocol.Abstractions.UnitTests` and
  `Surl.Core.UnitTests` for the contract's tests, `Documentation/Planning/Decisions` for ADR-0059
  (plus ADR-0006's "Amended by" line and the index row).
- Tests: `SmtpLimitTests.ServeAsync_IdleTimeout_Answers421TimeoutClosingThenCompletesWrites`,
  `ServeAsync_MaxExchangeDurationMidBody_Answers421TimeoutClosingStoresNothingAndCompletesWrites`,
  `ServeAsync_Shutdown_EndsWithNoFarewell`, `ServeAsync_FarewellNotReadWithinTheWriteDeadline_ClosesAndNotesIt`,
  `ServeAsync_ExchangeCancelledForALimitWhileALimitReplyWaits_StillClosesAtTheWriteDeadline`;
  `ServeAsync_ExchangeCancelledWhileALimitReplyWaits_Throws` became
  `ServeAsync_ShutdownWhileALimitReplyWaits_Throws`. `ExchangeContextTests` and the engine's idle,
  duration and shutdown tests assert the new members.
- Measure-CodeQuality: 30 libraries, 100% line and branch, 0 failing; Smtp 86 members, Core 152,
  Abstractions 97, worst CRAP 10.
- Follow-ups filed: BL-246 (FTP still writes 421 at shutdown; move it onto `IsCancelledForALimit`),
  BL-247 (IMAP's `* BYE` for the idle timeout and maximum duration).
## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. SMTP answers the idle timeout and maximum duration with 421 4.4.2 surl Timeout, closing; shutdown still ends with no farewell (ADR-0059)
