---
id: BL-2136
title: Correct the stale task references left in ADR-0132, ADR-0102 and the decisions index
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2129]
touches: [Documentation/Planning/Decisions/ADR-0132-how-the-account-book-changes-while-the-server-runs.md, Documentation/Planning/Decisions/ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2136 — Correct the stale task references left in ADR-0132, ADR-0102 and the decisions index

## Goal

What ADR-0132, ADR-0102 and the decisions index say about which tasks build what, and what is not yet built, is true.

## Context

Noticed while working BL-2133 on 2026-11-28, outside the places that task listed.

- `Documentation/Planning/Decisions/README.md`: the ADR-0132 row says "code by BL-1863 to BL-1859", a backwards
  range. ADR-0132 amendment 5 was built and documented by BL-1859 to BL-1866.
- ADR-0132 amendment 2 says "The code follows in BL-1602 ... and BL-1603", both done.
- ADR-0132 amendment 3 says the XML documents, `CLAUDE.md` and README "are BL-1841's to correct"; BL-1841 is done.
- Once BL-2129 is done: ADR-0102's two "not yet built (BL-2129)" statements and row 7 of the status table in
  ADR-0132 amendment 5 go stale.

## Acceptance criteria

- [ ] The decisions index's ADR-0132 row names its building tasks with no backwards range.
- [ ] ADR-0132 amendments 2 and 3 say, with a date, whether the work they name is done.
- [ ] ADR-0102 and ADR-0132 amendment 5 say of the empty mail view what the code does after BL-2129.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
