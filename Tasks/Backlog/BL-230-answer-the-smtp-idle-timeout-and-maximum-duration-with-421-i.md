---
id: BL-230
title: Answer the SMTP idle timeout and maximum duration with 421 in Surl.Protocol.Smtp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-198]
touches: [Surl.Protocol.Smtp.UnitLibrary, Surl.Protocol.Smtp.UnitTests, Surl.Protocol.Abstractions.UnitLibrary, Surl.Core.UnitLibrary]
requirement: FR-043
created: 2026-09-29
completed:
---
# BL-230 — Answer the SMTP idle timeout and maximum duration with 421 in Surl.Protocol.Smtp

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

- [ ] A fast test in `Surl.Protocol.Smtp.UnitTests` on a fake `TimeProvider` shows the idle
      timeout answered `421 4.4.2 surl Timeout, closing`, then writes completed; another shows
      the maximum exchange duration answered the same.
- [ ] Shutdown still ends an SMTP exchange with no bytes.
- [ ] `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for
      every library the task changes.

## Notes

## Log

- 2026-09-29: Created.
