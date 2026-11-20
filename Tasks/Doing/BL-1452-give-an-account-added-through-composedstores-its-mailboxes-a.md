---
id: BL-1452
title: Give an account added through ComposedStores its mailboxes and retire a removed account's
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1451]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1452 — Give an account added through ComposedStores its mailboxes and retire a removed account's

## Goal

An account added through `ComposedStores.AddAccount` (surl-mcp `add_account`, `Surl.Api`
`Accounts.Add`, control endpoint `POST /v1/accounts`) can `SELECT INBOX` over IMAP and receive
`seed_mail`. An account removed through `RemoveAccount` gets no more mail, as BL-1450's ADR says.

## Context

- Surly bug report `2026-11-19T2203-Surl-bug-report.md` items 1 and 2: `NO [NONEXISTENT]` and
  `NoSuchAccount` for an added account; delivery continuing to a removed one.
- `Application/Surl.Composition.UnitLibrary/ComposedStores.cs:623` (`AddAccount`) and `:630`
  (`RemoveAccount`) call only the `AccountBook`; this task also calls BL-1451's `MailboxStore` members
  on `mail` (the field at `:46`). `ComposedControlTarget.cs:176-185` replaces an account by removing then
  adding it, so a password change must not lose the owner's mail.
- An account restricted by `protocols=` to non-mail schemes, or a Bearer token's empty name, gets no
  owner, as `ReachOwners` skips them at start (the ADR confirms).

## Acceptance criteria

- [ ] A test in `Application/Surl.Composition.UnitTests` (`ComposedStoresTests` or a new
      `ComposedStoresMailOwnerTests`) adds `task-1` through `AddAccount` and then seeds mail to it with
      `SeedMailAsync`, which succeeds; `ListMailboxes("task-1")` lists `INBOX`.
- [ ] After `RemoveAccount("alice")` for a start-time account, `SeedMailAsync` to alice gives the ADR's
      result (`NoSuchAccount` expected) and `ListMailboxes` answers as the ADR says.
- [ ] `ComposedControlTarget.AddAccountAsync` for an existing name (a password change) keeps the
      owner's messages (named test).
- [ ] A whole-instance `ResetAsync` leaves the mail store and the accounts consistent (no owner without
      an account, no account without an owner) as the ADR says (named test).
- [ ] `dotnet build Application/Surl.Composition.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and coverage stays at 100% line and branch.

## Notes

- The surl-mcp and Surl.Api paths need no change of their own; a stand-in scenario test across both
  hosts, if wanted, is a separate task.

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-19T2203-Surl-bug-report.md` items 1 and 2.
- 2026-11-20: Backlog -> Doing.
