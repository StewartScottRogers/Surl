---
id: BL-2122
title: Keep mail views, recipients and maildrop locks from throwing after Reset drops their owner
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests, Documentation/Planning/Decisions/ADR-0193-what-mail-store-views-recipients-and-maildrop-locks-do-once-their-owner-is-dropped.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2122 — Keep mail views, recipients and maildrop locks from throwing after Reset drops their owner

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

- [x] A test in `Surl.MailStore.UnitTests` runs `RemoveMessages`, `LockMaildrop` and `Deliver` on handles to an owner `Reset` dropped, and none throws.
- [x] A test shows `CreateMailbox` on such a view is refused and leaves `ChangeCount` unchanged.
- [x] A test covers the same handles for an owner `Restore` dropped.
- [x] The XML comments of the operations changed state the outcome for a dropped owner.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Outcomes (ADR-0193, decided by Claude under Stewart's delegation): `CreateMailbox` is `MailboxMissing`;
  `LockMaildrop` gives an empty maildrop that locks nothing; `RemoveMessages` returns 0; a delivery leaves the
  dropped owner out and, with none left, is `Succeeded` with nothing stored or published (SMTP accepts and discards,
  as for no account; a new outcome would need an SMTP reply and would refuse the other recipients).
- A handle means that owner instance, not its name: a later owner of the same name is not reached; a `Restore` that
  brings the instance back is.
- `touches` gained the new ADR-0193 file and the decisions `README.md` index; no task in `Doing` names either.
  ADR-0185 was not edited, because BL-2123 (in `Doing`) touches it.
- Tests: `MailboxStoreDroppedOwnerTests` (11 failed before the fix). `Surl.MailStore.UnitTests` 474 pass; the whole
  fast run has no failure; `dotnet format --verify-no-changes` exits 0; `Measure-CodeQuality.ps1 -Library
  Surl.MailStore.UnitLibrary` reports 0 failing members.
- Follow-up filed: BL-2127 (`LookUpRecipient` and `ViewFor` read the accounts without the store's lock).

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. Mail views, recipients and maildrop locks of an owner Reset or Restore dropped give typed outcomes (ADR-0193) instead of throwing or writing to it
