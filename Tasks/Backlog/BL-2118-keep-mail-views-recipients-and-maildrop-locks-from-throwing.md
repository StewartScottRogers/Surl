---
id: BL-2118
title: Keep mail views, recipients and maildrop locks from throwing after Reset drops their owner
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests]
requirement: none
created: 2026-11-28
completed:
---
# BL-2118 — Keep mail views, recipients and maildrop locks from throwing after Reset drops their owner

## Goal

A view, recipient or maildrop lock taken before `Reset` or `Restore` dropped its owner gives a typed outcome, never
an exception and never a write into an owner the store no longer holds.

## Context

Found while working BL-2116 on 2026-11-28; the `Reset` path was reproduced with a scratch program.
`MailboxStore.EmptyOwner` (`Protocols/Mail/Surl.MailStore.UnitLibrary/MailboxStore.Control.cs`) clears a dropped
owner's mailboxes, and `MailboxStore.cs` later reads `owner.Inbox` for handles that still point at it.

- `AddOwner("bob")`; look up a recipient for `<bob@x>`; `Seed` one message; take `ViewFor("bob")` and
  `LockMaildrop`; then `Reset()`.
- `maildrop.RemoveMessages([1])`, `LockMaildrop(view)` and `Deliver([recipient], pending)` each throw
  `KeyNotFoundException` ('INBOX').
- `CreateMailbox(view, "ghost")` answers `Succeeded` for an owner the store no longer holds.

`Restore` drops owners through the same method; that path was not run. The `Reset` summary says a session holding a
maildrop lock removes no message at `QUIT`: keep that true. Decide the outcome each operation gives and record it in
an ADR if it settles behaviour ADR-0185 does not.

Stand-in (root `CLAUDE.md`): a test resets the store between scenarios while client sessions are still open.

## Acceptance criteria

- [ ] A test in `Surl.MailStore.UnitTests` runs `RemoveMessages`, `LockMaildrop` and `Deliver` on handles to an owner `Reset` dropped, and none throws.
- [ ] A test shows `CreateMailbox` on such a view is refused and leaves `ChangeCount` unchanged.
- [ ] A test covers the same handles for an owner `Restore` dropped.
- [ ] The XML comments of the operations changed state the outcome for a dropped owner.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-28: Created.
