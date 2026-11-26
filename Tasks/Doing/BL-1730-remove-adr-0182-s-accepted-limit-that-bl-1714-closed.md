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
completed:
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

- [ ] That bullet is removed, or states that BL-1714 closed the gap.
- [ ] No other statement in ADR-0182 claims the gap is open.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing.
