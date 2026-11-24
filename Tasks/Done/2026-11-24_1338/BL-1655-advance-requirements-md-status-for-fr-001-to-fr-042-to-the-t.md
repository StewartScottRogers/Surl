---
id: BL-1655
title: Advance Requirements.md status for FR-001 to FR-042 to the tests that prove them
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Product/Requirements.md]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-1655 — Advance Requirements.md status for FR-001 to FR-042 to the tests that prove them

## Goal

Each of FR-001 to FR-042 has a Status that says what is true now, and a row that is
proved names the test that proves it.

## Context

All 84 FR rows and every NFR row read `Draft`, though `Requirements.md:34` says a row
"stays `Draft` until that measurement is in a test" and the roadmap calls Phases 1 to 6
built and proven against pinned upstream curl. A reader cannot tell what is done. FR-015
(cookies) is genuinely unbuilt (BL-1650) and stays `Draft`. Audit, 2026-11-24.

## Acceptance criteria

- [x] The Status vocabulary is stated once above the table (e.g. `Draft`, `Proven`).
- [x] Every row from FR-001 to FR-042 moved off `Draft` names at least one existing test
      class that exercises it against pinned upstream curl.
- [x] FR-015 stays `Draft` and cites BL-1650.
- [x] No row's Requirement text changes.

## Notes

Status words: `Draft`, `Proven by <test classes>`. FR-031, FR-034, FR-035 stay `Draft`: no existing test runs pinned upstream curl against them (FR-035 is Surl-only).
BL-1656 does the rest of the table; both edit one file, so they run one after the other.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. FR-001..042 Status now Draft or Proven by named upstream-curl test classes; vocabulary stated
