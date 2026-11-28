---
id: BL-2130
title: Correct ADR-0132 and ADR-0102 on what removing an account does to its mail
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2123]
touches: [Documentation/Planning/Decisions/ADR-0132-how-the-account-book-changes-while-the-server-runs.md, Documentation/Planning/Decisions/ADR-0102-what-the-mail-file-message-and-group-options-mean-to-the-shared-stores.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2130 — Correct ADR-0132 and ADR-0102 on what removing an account does to its mail

## Goal

ADR-0132 says what a removal and a password change do to the mail store, and ADR-0102 points at the amendment that
narrows its decision 1.6.

## Context

Found while working BL-2123 on 2026-11-28. See ADR-0185 decisions 2 and 6 and its Amendment 1.

- ADR-0132 amendment 1, decision 3 and its "Documents this amendment changes" list say a password change still
  retires and re-reaches the mail owner. `ComposedStores.SetAccountPassword` leaves the mail owner as it is.
- ADR-0132 amendment 5 says the mail store "has retired the removed account". `MailboxStore.RetireOwner` retires
  nothing for a `--mailbox` account.
- ADR-0102 has no "Amended by" pointer to ADR-0185 Amendment 1, which narrows its decision 1.6.

## Acceptance criteria

- [ ] ADR-0132 no longer says a password change retires or re-reaches the mail owner.
- [ ] ADR-0132 amendment 5 says which removed accounts the mail store retires.
- [ ] ADR-0102 names ADR-0185 Amendment 1 as amending decision 1.6 and says it is not yet built, or, if BL-2129 is done by then, that it is built.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
