---
id: BL-1730
title: Remove ADR-0182's accepted limit that BL-1714 closed
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1714]
touches: [Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-1730 — Remove ADR-0182's accepted limit that BL-1714 closed

## Goal

ADR-0182 no longer says that an over-long first `HEADERS` frame leaves the following `CONTINUATION` unwithheld.

## Context

- File: `Documentation/Planning/Decisions/ADR-0182-surl-withholds-client-passwords-and-tokens-from-its-exchange-log-and-trace-dumps.md`.
- The last bullet of amendment 7's "Accepted limits" (about lines 243-244, ending "(BL-1714)") says an over-long first
  `HEADERS` frame does not yet make `Http2RequestBytesRedactor` withhold the `CONTINUATION` after it, here or in the
  origin's own log. BL-1714 made that false.

## Acceptance criteria

- [x] That bullet is removed, or states that BL-1714 closed the gap.
- [x] No other statement in ADR-0182 claims the gap is open.

## Notes

- Kept the amendment 7 bullet and marked it closed, matching the ADR's "Closed by amendment N" style.
- Amendment 8's accepted limits also listed the gap as pending on BL-1714; marked closed too (second criterion).
- Evidence: `Http2RequestBytesRedactor.LoseDecoderAndStartWithheldFrame` marks the decoder lost with or without a held
  frame; `Redact_ContinuationAfterAnOverTheCapHeadersFrame_WithholdsItsPayload` covers it.
- Other stale lines (BL-1738, BL-1735 shown as pending) are outside this task: filed as BL-1763.
- Documentation only: `dotnet build` clean (0 warnings), fast tests 0 failed.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
- 2026-11-25: Doing -> Done. ADR-0182 records the over-long HEADERS gap as closed by BL-1714 in amendments 7 and 8
