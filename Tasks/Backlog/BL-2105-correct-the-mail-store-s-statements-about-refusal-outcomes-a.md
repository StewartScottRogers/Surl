---
id: BL-2105
title: Correct the mail store's statements about refusal outcomes and published change events
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary]
requirement: none
created: 2026-11-28
completed:
---
# BL-2105 — Correct the mail store's statements about refusal outcomes and published change events

## Goal

The XML comments and `CLAUDE.md` of `Surl.MailStore.UnitLibrary` name every refusal outcome the mailbox writes can
return and say how many change events each operation publishes. No behaviour changes.

## Context

Found while working BL-2102, from reading `MailboxStore.cs` on 2026-11-28. Check each finding against the code before
changing a statement. All paths are under `Protocols/Mail/Surl.MailStore.UnitLibrary/`.

1. `<returns>` of `Deliver(IReadOnlyList<MailRecipient>, ...)` omits `MailStoreOutcome.QuotaExceeded`, returned for an
   owner's quota (`DeliverPending`) and for the out-of-space fault (`DeliverUnlessOutOfSpace`).
   `DeliverAsync(IReadOnlyList<MailRecipient>, ...)` takes its list from this one.
2. `<returns>` of `Append(..., PendingMessage, ...)` omits `QuotaExceeded` (`CheckRoomForOne` and the out-of-space
   fault). `Append` taking bytes and `AppendAsync` rely on that comment.
3. `<returns>` of `Copy` and `Move` omit `QuotaExceeded` (`CheckRoomToCopy`). A move within one owner adds nothing to
   it and needs no quota. Neither method consults the fault plan.
4. `<returns>` of `CreateMailbox`, `DeleteMailbox` and `RenameMailbox` omit `SharedFolderProtected`. The list of
   `CreateMailbox` also omits `FolderUseUnknown` and `FolderUseTaken`, which only its `use` parameter mentions.
5. `CLAUDE.md`, paragraph "Events and faults", says "Every change publishes one `StoreChangedEvent`". In the code,
   `Expunge` (`RemovePublished` in `MailboxStore.Events.cs`) and `Copy` (`CopyInto`) publish one per message, `Move`
   two per message, a rename of a mailbox other than `INBOX` two (a rename of `INBOX` one), a delivery to several owners
   one per owner copy, and `RetireOwner` none (`MailboxStore.Control.cs`). `AddOwner` reaching a retired owner again
   also publishes none. The same paragraph says "start-up is not a change", while `LoadAsync` counts an `INBOX` or configured folder
   it creates as a change and publishes nothing for it. The `events` parameter comments of the constructor and
   `LoadAsync` say "Where every change is published"; check them against the same facts.

Stand-in (root `CLAUDE.md`, "Surl is a stand-in for tests"): a test asserts on these outcomes and counts events on the
instance's event stream, so a missing outcome or a wrong count makes it expect the wrong thing. The task adds no store,
event, seed, reset or fault behaviour.

## Acceptance criteria

- [ ] The `<returns>` of `Deliver(IReadOnlyList<MailRecipient>, ...)` and of `Append(..., PendingMessage, ...)` name `QuotaExceeded` and say it covers both an owner's quota and the out-of-space fault.
- [ ] The `<returns>` of `Copy` and `Move` name `QuotaExceeded` for the destination owner's quota, and that of `Move` says a move within one owner is not refused for quota.
- [ ] The `<returns>` of `CreateMailbox`, `DeleteMailbox` and `RenameMailbox` name `SharedFolderProtected` and when each returns it, and that of `CreateMailbox` also names `FolderUseUnknown` and `FolderUseTaken`.
- [ ] The "Events and faults" paragraph of the project's `CLAUDE.md` no longer says every change publishes one event; it states how many `StoreChangedEvent`s each case in finding 5 publishes.
- [ ] No statement in that paragraph or in the `events` parameter comments says or implies that a change made at start-up, or one made by `RetireOwner`, publishes an event.
- [ ] `dotnet build Protocols/Mail/Surl.MailStore.UnitLibrary -warnaserror` is clean, and the diff changes only XML comments and `CLAUDE.md` in that project (and this task's own file).

## Notes

## Log

- 2026-11-28: Created.
