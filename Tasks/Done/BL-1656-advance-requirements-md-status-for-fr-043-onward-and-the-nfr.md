---
id: BL-1656
title: Advance Requirements.md status for FR-043 onward and the NFR rows to the tests that prove them
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1655]
touches: [Documentation/Product/Requirements.md]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-1656 — Advance Requirements.md status for FR-043 onward and the NFR rows to the tests that prove them

## Goal

Each FR row from FR-043 on and each NFR row has a Status that says what is true now, in
the vocabulary BL-1655 states.

## Context

See BL-1655. Extended-server rows (FR-066 to FR-084) are proved by their spec-replay unit
tests and the Curl port's `Integration` tests, never by upstream curl (ADR-0083); the
Status must say which. NFR-006 still lists "Not yet proven" items (no release tagged,
BL-1657).

## Acceptance criteria

- [x] Every row from FR-043 on moved off `Draft` names an existing test class.
- [x] FR-066 to FR-084 state their ADR-0083 proof, not an upstream curl one.
- [x] NFR-001 to NFR-005 and NFR-007 cite the gate that enforces them
      (`CodeMetricsConfig.txt`, `Measure-CodeQuality.ps1`, CI).
- [x] NFR-006 stays short of proven while BL-1657 is open, and says so.

## Notes

- FR-043 to FR-059, FR-062 and FR-065 now read `Proven by` the existing upstream-curl test classes; FR-056 and FR-066 to FR-084 state the ADR-0083 route (spec-replay tests plus the Curl port client `Integration` class, never upstream curl). FR-060, FR-061, FR-063 and FR-064 stay `Draft` with what they wait on: no test runs upstream curl against the event stream, every store, hooks or determinism.
- NFR-001 to NFR-005 and NFR-007 read `Enforced by` their gate (new status word for non-functional rows, stated in the NFR intro). NFR-006 stays `Draft [BL-1657]`. NFR-008 to NFR-020 are outside this task and unchanged.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. FR-043 onward and NFR-001 to NFR-007 statuses advanced to the tests and gates that prove them.
