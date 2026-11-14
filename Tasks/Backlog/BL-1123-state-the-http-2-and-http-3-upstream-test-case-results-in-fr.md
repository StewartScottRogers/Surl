---
id: BL-1123
title: State the HTTP/2 and HTTP/3 upstream test-case results in FR-053 to FR-055 and Milestone 6
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1121, BL-1122]
touches: [Documentation/Planning/Roadmap.md, Documentation/Product/Requirements.md, Documentation/Product/Product-Overview.md]
requirement: FR-053
created: 2026-11-14
completed:
---
# BL-1123 — State the HTTP/2 and HTTP/3 upstream test-case results in FR-053 to FR-055 and Milestone 6

## Goal

FR-053, FR-054 and FR-055 in `Documentation/Product/Requirements.md`, the Milestone 6 entry in
`Documentation/Planning/Roadmap.md`, and the product overview's "Built for Phase 6" section all
state the HTTP/2 and HTTP/3 upstream test-case results that BL-1121 measured, and ADR-0154 is cited.

## Context

- BL-1113 decided how the http/2 and http/3 halves play nghttpx (ADR-0154). BL-1114 to BL-1119 built
  them, BL-1120 composed the mail-TLS halves, and BL-1121 recorded the CI run's pass rates. Use the
  numbers from `Documentation/Conformance/Upstream-Test-Case-Pass-Rate.md` as BL-1121 left it.
- Statements to replace:
  - FR-053's "cases whose server half is not built (`http/2`, `http/3`, …)" and its
    `server-half-not-composed` list;
  - FR-054's and FR-055's "any upstream test case over HTTP/2 / HTTP/3, since the … server half is
    not built";
  - Milestone 6's "No upstream test case runs over HTTP/2 or HTTP/3, since those server halves are
    not built".
- Say which of tests 1700, 1701, 1702, 2403, 2500, 2503 and 987 to 989 pass on which pin, and name
  the follow-up task for each that does not. Say why the rest of the 16 http/2 and http/3 cases
  never run (Debug or libtest), as ADR-0154 lists them.
- `Documentation/Wiki/Glossary.md` gets an entry only if ADR-0154 introduced a new term, such as
  "gateway half". That file is then a fourth touch, so add it to `touches` before starting.

## Acceptance criteria

- [ ] FR-053, FR-054 and FR-055 cite ADR-0154, give the measured outcome for each of the nine cases
      above on `linux-x64` and `osx-arm64`, and no longer say the http/2 or http/3 server half is
      not built.
- [ ] Roadmap Milestone 6's Status and Delivers lines say the same, dated, and list ADR-0154 under
      Decisions.
- [ ] `Product-Overview.md`'s "Built for Phase 6" section mentions the gateway halves and their
      pass rate in one paragraph that is consistent with the two files above.
- [ ] `git grep -n "server half is not built" Documentation/` returns nothing about `http/2` or
      `http/3`.

## Notes

## Log

- 2026-11-14: Created.
