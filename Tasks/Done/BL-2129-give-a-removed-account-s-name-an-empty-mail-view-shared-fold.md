---
id: BL-2129
title: Give a removed account's name an empty mail view, shared folders included
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2123]
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests, Application/Surl.Composition.UnitLibrary/ComposedStores.cs, Documentation/Planning/Decisions/ADR-0185-the-mail-store-reaches-an-account-added-while-the-server-runs-and-retires-one-removed.md, Documentation/Planning/Decisions/README.md, Documentation/Wiki/Glossary.md]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2129 — Give a removed account's name an empty mail view, shared folders included

## Goal

A view opened for an account removed while the server runs holds no mailbox and no shared folder until an account of
that name that may log in on a mail scheme is added again (ADR-0185 Amendment 1).

## Context

Decided in BL-2123: ADR-0185 Amendment 1 is the specification and is not yet built.

- `MailboxStore.ViewFor` (`Protocols/Mail/Surl.MailStore.UnitLibrary/MailboxStore.cs`) selects shared folders by
  member name alone, so a Bearer-token login, a `--user-ldap` login or a host mail read under a removed name still
  reaches team folders.
- A removed `--mailbox` account also keeps its own mailboxes: `RetireOwner` (`MailboxStore.Control.cs`) returns
  `false` for it and records nothing.
- The store has to remember removals. What `AddOwner` and `RetireOwner` return, and delivery, do not change.
- `ComposedStores.RemoveAccount` already calls `RetireOwner` on every removal; only its remarks need to change.
- `MailboxStoreRunTimeOwnerTests` has a test named `...AndEmptyToViewFor` whose view assertion checks only
  `OwnerName`, so a member's view would pass it without being empty. Make the name and the assertion agree.

Stand-in (root `CLAUDE.md`): a test revokes an account mid-run, sees its team access end, then adds it back.

## Acceptance criteria

- [x] Tests in `Surl.MailStore.UnitTests` show that after `RetireOwner` of a shared-folder member, and of a `--mailbox` account, `ViewFor` gives no mailbox and no shared folder (reading `Shared/<name>` is `MailboxMissing`), while the `--mailbox` address stays `Deliverable`.
- [x] A test shows `AddOwner` of the removed name gives back its mailboxes and shared folders, with the return values ADR-0185 decision 6 states.
- [x] Tests show a directory-only member never removed still reaches its folders (one with an unreached owner loaded from files included), and a view and a recipient given before the removal still work.
- [x] A test shows `Reset` forgets removals and `Restore` puts back those in force at the snapshot.
- [x] The XML comments, the project's `CLAUDE.md`, the `ComposedStores.RemoveAccount` remarks, ADR-0185, its index row and the glossary describe the new behaviour and no longer say "not yet built".
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- `MailboxStore` remembers removals in a name set that `RetireOwner` adds to (whatever it returns), `AddOwner` and
  `Reset` clear, and `MailStoreSnapshot` carries. New tests: `MailboxStoreRemovedAccountViewTests`.
- `RetireOwner` records any name it is given, a name with no owner included: the composition calls it only for an
  account it removed, and a removed member with no owner must lose its folders too.
- An `AddOwner` that throws (no `UIDVALIDITY` left) keeps the removal, as "nothing is changed" asks.
- `ViewFor` now reads under `storeLock`, because the new set is changed under it. That is the `ViewFor` half of
  BL-2131; `LookUpRecipient` and the router are left to that task.
- No upstream curl measurement: ADR-0185 Amendment 1 adds no byte.
- 2026-11-28 checks: build 0 warnings, fast tests pass, format clean, `Measure-CodeQuality.ps1 -Library
  Surl.MailStore.UnitLibrary` 0 failing members.

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. A removed account's name gets an empty mail view, shared folders included, until it is added again
