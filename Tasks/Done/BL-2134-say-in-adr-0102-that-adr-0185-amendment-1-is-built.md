---
id: BL-2134
title: Say in ADR-0102 that ADR-0185 Amendment 1 is built
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2129]
touches: [Documentation/Planning/Decisions/ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2134 — Say in ADR-0102 that ADR-0185 Amendment 1 is built

## Goal

ADR-0102 says that ADR-0185 Amendment 1 is built, and describes what `MailboxStore.ViewFor` does for a removed name.

## Context

Filed from BL-2130, which added an "Amended by" bullet and a note at the top of decision 1.6 while BL-2129 was still
being worked. Both say "not yet built", and the note ends with an "As the code is on 2026-11-28" sentence saying a
removed member's view still holds the shared folders. BL-2129 builds the rule and does not touch ADR-0102.

## Acceptance criteria

- [x] ADR-0102 no longer says ADR-0185 Amendment 1 is not yet built, in its header or in decision 1.6.
- [x] The note at the top of decision 1.6 states what `MailboxStore.ViewFor` does for a removed name, as the code is.

## Notes

- 2026-11-28: Edited ADR-0102 directly rather than through `align-and-document`: two passages in one file. The note
  was written from `MailboxStore.ViewFor`, `AddOwner`, `RetireOwner`, `Reset` and `Restore`, and from
  `ComposedStores.AddAccount` and `RemoveAccount`, read in the source. No new decision, so no new ADR.
- 2026-11-28: Row 7 of ADR-0132 amendment 5's status table still says the empty mail view is not built. It is
  outside this task's `touches`, and BL-2136 (Backlog) already covers it, so nothing was filed. BL-2136's two
  ADR-0102 "not yet built" statements are the ones corrected here.
- 2026-11-28: `dotnet build` clean (0 warnings); fast tests: 82 assemblies passed, 30848 passed, 3 skipped. The
  change is Markdown only, so `dotnet format` was not run.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. ADR-0102's header and decision 1.6 say ADR-0185 Amendment 1 is built and what MailboxStore.ViewFor does for a removed name
