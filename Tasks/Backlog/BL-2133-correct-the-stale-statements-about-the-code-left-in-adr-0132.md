---
id: BL-2133
title: Correct the stale statements about the code left in ADR-0132 and ADR-0102
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0132-how-the-account-book-changes-while-the-server-runs.md, Documentation/Planning/Decisions/ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2133 — Correct the stale statements about the code left in ADR-0132 and ADR-0102

## Goal

Every statement ADR-0132 and ADR-0102 make about the code in the places listed below is true of the code.

## Context

Noticed while working BL-2130 on 2026-11-28; check each against the code before changing it.

- ADR-0132 amendments 4 and 5: the "Status of the rule" bullets and the "today" table entries say the code does not
  yet follow the rule. `RequiresLoginWithoutAccounts` and `IsLoginRequiredToRead` suggest it now does.
- ADR-0132 amendment 5: the ranges "BL-1863 to BL-1859" and "BL-1865 to BL-1859" run backwards.
- ADR-0132 amendment 1, last bullet of "Documents this amendment changes": says `IControlTarget`'s documents still
  change with later alignment work.
- ADR-0102 decision 2.3 table, HTTP row: says the HTTP server takes no uploads. `HttpRequestAnswerer.Uploads.cs`
  accepts `PUT` under `--allow-uploads`.
- ADR-0102 decision 3.5: says Redis pub/sub is left to BL-678. `TopicNameLookup.cs` maps `redis` and `rediss`.

## Acceptance criteria

- [ ] ADR-0132 amendments 4 and 5 say of each rule whether the code follows it, with a date.
- [ ] ADR-0132 amendment 5 names its building tasks as IDs that exist on the board, with no backwards range.
- [ ] The `IControlTarget` bullet in ADR-0132 amendment 1 matches `IControlTarget`'s documentation comments.
- [ ] ADR-0102 decision 2.3's HTTP row says what a `PUT` gets with and without `--allow-uploads`.
- [ ] ADR-0102 decision 3.5 says how Redis pub/sub channel names map, as `TopicNameLookup` does.

## Notes

## Log

- 2026-11-28: Created.
