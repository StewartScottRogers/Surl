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
completed:
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

- [ ] A fast test in `Surl.Protocol.Imap.UnitTests` on a fake `TimeProvider` shows the idle
      timeout answered `* BYE surl Timeout, closing`, then writes completed; another shows the
      maximum exchange duration answered the same.
- [ ] A fast test shows shutdown (both tokens cancelled) ends the exchange with no `BYE`.
- [ ] `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for
      `Surl.Protocol.Imap.UnitLibrary`.

## Notes

## Log

- 2026-09-30: Created.
