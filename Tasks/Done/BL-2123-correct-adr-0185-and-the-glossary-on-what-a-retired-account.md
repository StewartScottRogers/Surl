---
id: BL-2123
title: Correct ADR-0185 and the glossary on what a retired account still reaches
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-0185-the-mail-store-reaches-an-account-added-while-the-server-runs-and-retires-one-removed.md, Documentation/Planning/Decisions/README.md, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-11-28
completed: 2026-11-28
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

- [x] ADR-0185 states, amended and marked "Decided by Claude under Stewart's delegation", whether a retired shared-folder member keeps its shared folders.
- [x] ADR-0185 decision 2 no longer claims `NoSuchAccount` or "stores nothing" for a path a catch-all, alias, mailbox or case-variant account delivers.
- [x] The glossary's "owner" and "view" entries include the shared folders a view reaches.
- [x] Any behaviour the decision changes is filed as a task on the board and named in the ADR as not yet built.

## Notes

- Decision (ADR-0185 Amendment 1, by Claude under Stewart's delegation): a retired shared-folder member does **not**
  keep its shared folders. Secure by default: removing an account takes its access away, and a Bearer token or a
  directory entry that outlives the account should not reach team mail. It is intent, not yet built: BL-2127.
- The amendment treats a removed `--mailbox` account the same (empty view, address still receives mail), or a removed
  member could keep both its own mail and its shared folders. A directory-only member never removed keeps access.
- Also corrected in ADR-0185, checked against the code: a password change retires nothing (decision 2), which accounts
  get an owner (decision 6), and which adds raise `ChangeCount` (decision 7).
- Added `Documentation/Planning/Decisions/README.md` to `touches`, for the index row to name the amendment; no task
  in `Doing` named it.
- Drift found outside this task: ADR-0132 and ADR-0102 filed as BL-2128; the misnamed assertion in
  `MailboxStoreRunTimeOwnerTests` is in BL-2127's Context.
- Documentation only, no `.cs` or project file changed, so no `verify` run was needed by the `docs` pipeline.
  `dotnet build` was clean (0 warnings, 0 errors) and the fast tests showed no failure.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. ADR-0185 and the glossary say what a retired account still reaches; Amendment 1 decides a removed account keeps no mail view, filed as BL-2127
