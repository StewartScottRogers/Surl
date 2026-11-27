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
completed: 2026-11-26
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

- [x] FR-054's "Satisfied by" list gives the number of test methods
      `UpstreamCurlFetchesFromSurlOverHttp2Tests` holds when the task is done.
- [x] FR-054 contains "the server's `SETTINGS`" and no "the servers `SETTINGS`".
- [x] FR-055 cites ADR-0082's Amendments 1 to 7, or every amendment the ADR holds if there are more.
- [x] FR-057's statements about the extended clients' tests agree with each other and with ADR-0089.

## Notes

Verified on 2026-11-26:

- HTTP/2 count: 17. `UpstreamCurlFetchesFromSurlOverHttp2Tests.cs` holds 17 `[TestMethod]`
  attributes, each on one test method, and no `DataRow`, `DataTestMethod` or `DynamicData`.
  The class is not partial. At `424106dd` it held 15, so the CI run 42324374116 sentence is
  unchanged.
- "the server's `SETTINGS`" replaces the only "the servers `SETTINGS`".
- ADR-0082 has exactly seven `## Amendment` headings, 1 to 7.
- FR-057: ADR-0089 decision 9 and Amendment 9's table put the extended clients outside Phase 7;
  their tests stay `Integration` tests of the extended track. They are removed from the list of
  measurements, and the "outside Phase 7" sentence now names them. The four
  `PinnedCurlPortClientTalksToSurlOver*Tests` classes each carry `TestCategory("Integration")`.
- Delivered by `align-and-document`. No `.cs` or project file changed; `dotnet build` is clean
  (0 warnings) and the fast tests pass.
- Follow-ups filed: BL-1805 (FR-054's AWS SigV4 HTTP/2 proof, FR-057's libcurl-row task and the
  paragraph under the table) and BL-1806 (ADR-0082's amendment list and numbering).

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing.
- 2026-11-26: Doing -> Done. FR-054, FR-055 and FR-057 state the current HTTP/2 test count, ADR-0082 amendment range and extended-client test status
