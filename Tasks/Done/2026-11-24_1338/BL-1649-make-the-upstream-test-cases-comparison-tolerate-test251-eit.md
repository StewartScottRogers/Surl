---
id: BL-1649
title: Make the upstream-test-cases comparison tolerate test251 either way (ADR-0089 amendment 10)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Conformance/Surl.Conformance.UnitLibrary, Conformance/Surl.Conformance.UnitTests]
requirement: FR-057
created: 2026-11-24
completed: 2026-11-24
---
# BL-1649 — Make the upstream-test-cases comparison tolerate test251 either way (ADR-0089 amendment 10)

## Goal

`UpstreamTestCasePortComparison` judges test251 either way (timeout shown or case finished), per ADR-0089 amendment 10, so the linux-x64 result is stable.

## Context

ADR-0089 amendment 10 (BL-1647) and UTC-4 in `Upstream-Test-Cases.md`. Only test251, only when upstream passes it; any other difference is judged as before. Check the library name under Conformance first.

## Acceptance criteria

- [x] A unit test shows test251 passes the comparison both with the timeout row and with a finished (both sections pass) row, and a different failure still fails.

## Notes

Upstream must pass test251 and the port show `timeout` for the finding to count; the port finishing is not stale; any other port failure is an unlisted defect. Only case 251 is tolerant (ADR-0089 amendment 10). Four unit tests in UpstreamTestCasePortComparisonTests.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing.
- 2026-11-24: Doing -> Done. test251 judged either way; tests green
