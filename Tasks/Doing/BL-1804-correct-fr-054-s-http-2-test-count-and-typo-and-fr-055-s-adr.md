---
id: BL-1804
title: Correct FR-054's HTTP/2 test count and typo and FR-055's ADR-0082 amendment range in the requirements
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Product/Requirements.md]
requirement: FR-054
created: 2026-11-26
completed:
---
# BL-1804 — Correct FR-054's HTTP/2 test count and typo and FR-055's ADR-0082 amendment range in the requirements

## Goal

FR-054 and FR-055 state the current HTTP/2 test count and ADR-0082 amendment range, FR-054 has no
missing apostrophe, and FR-057 agrees with itself about the extended clients' tests.

## Context

Found by BL-1802, all in `Documentation/Product/Requirements.md`. Checked on 2026-11-26:

- FR-054's "Satisfied by" list says "the 15 tests of `UpstreamCurlFetchesFromSurlOverHttp2Tests`".
  The class has 17 test methods; commit `1b4d7c853` (BL-1549) changed it. The sentence about CI run
  42324374116 names the 15 tests at head `424106dd` and stays true.
- FR-054 says "the servers `SETTINGS`" where it means "the server's `SETTINGS`".
- FR-055 says "Amendments 1 to 6". ADR-0082 also has Amendment 7 (BL-1100).

Not checked: FR-057 lists the extended clients' `PinnedCurlPortClientTalksToSurlOver*Tests` among
"the measurements" BL-580 to BL-591, and later says "the extended clients are outside Phase 7
(decision 9)". Read ADR-0089 to see which reading is intended.

No stand-in store, event or fault applies: this task changes prose only.

## Acceptance criteria

- [ ] FR-054's "Satisfied by" list gives the number of test methods
      `UpstreamCurlFetchesFromSurlOverHttp2Tests` holds when the task is done.
- [ ] FR-054 contains "the server's `SETTINGS`" and no "the servers `SETTINGS`".
- [ ] FR-055 cites ADR-0082's Amendments 1 to 7, or every amendment the ADR holds if there are more.
- [ ] FR-057's statements about the extended clients' tests agree with each other and with ADR-0089.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
