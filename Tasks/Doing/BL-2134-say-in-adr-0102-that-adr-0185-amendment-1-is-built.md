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
completed:
---
# BL-2134 — Say in ADR-0102 that ADR-0185 Amendment 1 is built

## Goal

ADR-0102 says that ADR-0185 Amendment 1 is built, and describes what `MailboxStore.ViewFor` does for a removed name.

## Context

Filed from BL-2130, which added an "Amended by" bullet and a note at the top of decision 1.6 while BL-2129 was still
being worked. Both say "not yet built", and the note ends with an "As the code is on 2026-11-28" sentence saying a
removed member's view still holds the shared folders. BL-2129 builds the rule and does not touch ADR-0102.

## Acceptance criteria

- [ ] ADR-0102 no longer says ADR-0185 Amendment 1 is not yet built, in its header or in decision 1.6.
- [ ] The note at the top of decision 1.6 states what `MailboxStore.ViewFor` does for a removed name, as the code is.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
