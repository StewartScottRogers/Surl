---
id: BL-1714
title: Fail closed after an oversized HTTP/2 header frame in Http2RequestBytesRedactor
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Http/Surl.Http2.UnitLibrary, Protocols/Http/Surl.Http2.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1714 — Fail closed after an oversized HTTP/2 header frame in Http2RequestBytesRedactor

## Goal

After any `HEADERS` or `CONTINUATION` frame that exceeds the 64 KiB cap, `Http2RequestBytesRedactor` withholds every
later header-block payload on the connection.

## Context

- Found by the review of BL-1709, by reading the code; not yet reproduced by a test.
- In `StartFrame`, the over-limit branch calls `LoseDecoderAndWriteHeldFrames`, which returns early when no frame is
  held, so `decoderLost` stays false. A single `HEADERS` frame over the cap with no `END_HEADERS` is then followed by a
  `CONTINUATION` decoded as a block of its own against a stale table, and its bytes can be logged raw.
- ADR-0182 amendment 3 says "from then on every header-block payload on that connection is withheld".

## Acceptance criteria

- [x] A named fast test in `Surl.Http2.UnitTests` sends an over-limit `HEADERS` frame without `END_HEADERS`, then a
      `CONTINUATION`, and finds the continuation's payload logged as `****`.
- [x] Build with `-warnaserror` is clean, fast tests are green, and `Surl.Http2.UnitLibrary` stays at 100% line and
      branch coverage.

## Notes

- Test: `Redact_ContinuationAfterAnOverTheCapHeadersFrame_WithholdsItsPayload`. It failed before the fix (the
  continuation's `0x82` was logged as received).
- Fix: `LoseDecoderAndWriteHeldFrames`, renamed `LoseDecoderAndWriteHeldFramesWithheld`, now always loses the decoder;
  the "is a block open" condition moved to the non-header-block branch of `StartFrame`, its only caller that needs it.
- The class remarks were reworded to state what the code does, including the sequence checks it does not make.
- Verified 2026-11-25: build 0 warnings, format clean, fast tests green (186 in `Surl.Http2.UnitTests`),
  `Measure-CodeQuality.ps1 -Library Surl.Http2.UnitLibrary` 100% line and branch, 0 failing members.
- Conformance stage skipped: only the exchange log changes, no option, exit code or wire byte.
- ADR-0182 amendment 7's last accepted limit names this task as open and is now false. It is not edited here: BL-1713,
  in `Doing`, touches that file, and no acceptance criterion needs it. Filed as BL-1718. No new decision, so no ADR.
- Follow-ups: BL-1716 (sequence-breaking frames and `PUSH_PROMISE`), BL-1717 (bound the held empty frames), BL-1718.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. Http2RequestBytesRedactor withholds every header-block payload after an over-cap HEADERS or CONTINUATION frame, with none held or some
