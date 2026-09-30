---
id: BL-278
title: Retire MailboxStore.Deliver's byte-span overload now that no mail server calls it
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.MailStore.UnitLibrary, Surl.MailStore.UnitTests, Surl.Protocol.Pop3.UnitTests]
requirement: FR-043
created: 2026-09-30
completed: 2026-09-30
---
# BL-278 — Retire MailboxStore.Deliver's byte-span overload now that no mail server calls it

## Goal

`MailboxStore` has one way in for a delivered message, `Deliver(IReadOnlyList<MailRecipient>, PendingMessage)`: the `ReadOnlySpan<byte>` overload, the bytes it holds in memory until the next save, and the `Surl.MailStore.UnitLibrary/CLAUDE.md` paragraph that keeps it "for the servers not yet streaming" are gone.

## Context

- Found by BL-271 (2026-09-30): SMTP's `DATA` now streams into a `PendingMessage`, so no mail
  server calls `MailboxStore.Deliver(IReadOnlyList<MailRecipient>, ReadOnlySpan<byte>)`
  (`grep -rn "Deliver(" Surl.Protocol.*.UnitLibrary` to confirm before removing it).
- ADR-0050 decision 7: a message file is written once through `messages/.pending-<guid>`,
  streamed as the server reads it. The span overload's hold-in-memory path (`HoldBody`, BL-191's
  shape) is the only exception left, and only tests exercise it.
- Tests in `Surl.MailStore.UnitTests` that deliver through the span overload move to the
  `PendingMessage` overload (write the bytes to `CreatePendingMessage().Body`), keeping what they
  pin; tests of held bodies that only the span overload reaches go with it.

## Acceptance criteria

- [x] `MailboxStore` has no `Deliver` overload taking a byte span or array, and no code path that
      holds a delivered message's bytes in memory for a later save when the store has files.
- [x] `Surl.MailStore.UnitLibrary/CLAUDE.md` no longer mentions the span overload.
- [x] `dotnet build -warnaserror` is clean, the fast tests pass, and `Surl.MailStore.UnitLibrary`
      keeps 100% line and branch coverage.

## Notes

- Delivered directly rather than through the full `/feature` stages: a removal with no new
  behaviour and no wire bytes, so no plan or conformance run had anything to decide.
- Removed with the overload: `HoldBody`, the `unwrittenBodies` set, `MarkWritten`, the save's
  message-file write loop and `MailStoreFiles.WriteMessageAsync`; `MessageBody.Bytes` is now
  get-only (only a store without files holds bytes). The span `Append` overload stays: it
  already routes through a pending message, and the task names only `Deliver`.
- Tests: `MailStoreFixture.Deliver` and the other span callers now write to a
  `PendingMessage` (`MailStoreFixture.Pending`). Deleted as reachable only through held bodies:
  `Deliver_NoRecipients_StoresNothing` and `Deliver_NullRecipients_Throws` (the pending overload's
  twins in `MailboxStorePendingMessageTests` pin both), and
  `SaveChangesAsync_MessageRemovedWhileItsFileIsBeingWritten_...` and
  `SaveChangesAsync_MessageFileWriteThrows_...` with `UnitTestFaultingContentFileSystem.BeforeMoveTo`.
- `touches` gained `Surl.Protocol.Pop3.UnitTests` (no task in Doing names it): its
  `Pop3TestExchange.Seed` delivered through the span overload. Its store-write-failure tests
  seeded a store whose file system failed every write, which only the held path survived, so
  `UnitTestUnwritableContentFileSystem` became `UnitTestWriteFailingContentFileSystem`: it
  accepts and forgets writes until `FailsWrites` is set after seeding.
- Coverage (`Measure-CodeQuality.ps1 -Library Surl.MailStore.UnitLibrary`): 100% line,
  100% branch, max complexity 10.

## Log

- 2026-09-30: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. MailboxStore.Deliver takes only a PendingMessage; the held-in-memory delivery path is gone
