---
id: BL-277
title: Retire MailboxStore.Deliver's byte-span overload now that no mail server calls it
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.MailStore.UnitLibrary, Surl.MailStore.UnitTests]
requirement: FR-043
created: 2026-09-30
completed:
---
# BL-277 — Retire MailboxStore.Deliver's byte-span overload now that no mail server calls it

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

- [ ] `MailboxStore` has no `Deliver` overload taking a byte span or array, and no code path that
      holds a delivered message's bytes in memory for a later save when the store has files.
- [ ] `Surl.MailStore.UnitLibrary/CLAUDE.md` no longer mentions the span overload.
- [ ] `dotnet build -warnaserror` is clean, the fast tests pass, and `Surl.MailStore.UnitLibrary`
      keeps 100% line and branch coverage.

## Notes

## Log

- 2026-09-30: Created.
