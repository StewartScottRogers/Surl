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
completed: 2026-11-28
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

- [x] ADR-0132 amendments 4 and 5 say of each rule whether the code follows it, with a date.
- [x] ADR-0132 amendment 5 names its building tasks as IDs that exist on the board, with no backwards range.
- [x] The `IControlTarget` bullet in ADR-0132 amendment 1 matches `IControlTarget`'s documentation comments.
- [x] ADR-0102 decision 2.3's HTTP row says what a `PUT` gets with and without `--allow-uploads`.
- [x] ADR-0102 decision 3.5 says how Redis pub/sub channel names map, as `TopicNameLookup` does.

## Notes

- Delivered by `align-and-document`, 2026-11-28. Amendments 4 and 5 each carry a table, one row per decision,
  read in the source (not run). Both say the code follows the rule; the one exception recorded is the empty mail
  view amendment 5 decision 7 points to, not built (BL-2129). The old "What the source does" sections are kept as
  history, in the past tense.
- The ranges now read BL-1859 to BL-1866 (all in `Tasks/Done`), and the warning table names BL-1865, BL-1866 and
  BL-1859.
- The sentence under ADR-0102 decision 2.3's table claimed every original-server reply was measured. ADR-0133
  records no measurement of the `403` to a `PUT`, so it was measured: `Record-CurlExchange.ps1`, reference
  win-x64 curl 8.21.0, `-sS -u user:secret -T <16 bytes>` answered `403` with `Content-Length: 0`: no output,
  exit 0. The sentence now says so.
- No new ADR: no decision changed.
- Filed BL-2137 (an upload to a hidden share's bare name appears to reveal it) and BL-2136 (stale statements
  outside the listed places, the decisions index's backwards range among them).

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. ADR-0132 and ADR-0102 now state what the code does in each listed place; follow-ups filed as BL-2137 and BL-2136
