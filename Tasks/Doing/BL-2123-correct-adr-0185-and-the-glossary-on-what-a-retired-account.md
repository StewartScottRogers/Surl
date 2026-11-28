---
id: BL-2123
title: Correct ADR-0185 and the glossary on what a retired account still reaches
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0185-the-mail-store-reaches-an-account-added-while-the-server-runs-and-retires-one-removed.md, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-11-28
completed:
---
# BL-2123 — Correct ADR-0185 and the glossary on what a retired account still reaches

## Goal

ADR-0185 and the glossary say what a retired account still reaches in the mail store, and the ADR records whether
shared-folder access after retirement is intended.

## Context

Found while working BL-2116 on 2026-11-28, which corrected the comments of `Surl.MailStore.UnitLibrary` (see its
Notes, and `RetireOwner` in `MailboxStore.Control.cs`).

1. ADR-0185 decision 2 says `ViewFor` refuses a retired name, `LookUpRecipient` answers `NoSuchAccount` and `Seed`
   stores nothing. As the code is, a retired shared-folder member still lists and appends to its shared folders, and a
   path naming it can still be delivered through an alias or mailbox of that address, an account differing only by
   case, or a mail domain's catch-all.
2. `Documentation/Wiki/Glossary.md`, "owner", says a name that names no owner gets an empty view. A shared-folder
   member does not.
3. The "view" entry leaves out shared folders.

Decide, under Stewart's delegation (root `CLAUDE.md`, "Decisions"), whether shared-folder access after retirement is
intended. If it is a defect, record that in the ADR as intent and file a code task for it.

## Acceptance criteria

- [ ] ADR-0185 states, amended and marked "Decided by Claude under Stewart's delegation", whether a retired shared-folder member keeps its shared folders.
- [ ] ADR-0185 decision 2 no longer claims `NoSuchAccount` or "stores nothing" for a path a catch-all, alias, mailbox or case-variant account delivers.
- [ ] The glossary's "owner" and "view" entries include the shared folders a view reaches.
- [ ] Any behaviour the decision changes is filed as a task on the board and named in the ADR as not yet built.

## Notes

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
