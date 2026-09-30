---
id: BL-225
title: Name MailboxStore in ADR-0050 decision 6's bounds table
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-190]
touches: [Documentation/Planning/Decisions/ADR-0050-the-mail-store-and-the-line-machinery-the-mail-servers-share.md]
requirement: none
created: 2026-09-29
completed:
---
# BL-225 — Name MailboxStore in ADR-0050 decision 6's bounds table

## Goal

ADR-0050 names the mail store's type as the code has it, `MailboxStore`, so the document is
true of the code.

## Context

- BL-190 built the store as `Surl.MailStore.MailboxStore`, not `MailStore`: a type named
  `MailStore` inside the namespace `Surl.MailStore` is shadowed by that namespace in every
  other `Surl.*` namespace (C# name lookup finds the namespace `Surl.MailStore` before any
  `using`-imported type), so each server would have to write `global::Surl.MailStore.MailStore`.
- ADR-0050 decision 6's table still names `MailStore.DefaultMaxMessages`,
  `MailStore.DefaultMaxTotalMessageBytes`, `MailStore.DefaultMaxMailboxes` and
  `MailStore.MaxMailboxNameBytes`. The constants exist, with those values, on `MailboxStore`.
- BL-190 also returns `MailStoreOutcome.MessageTooLarge` for a message past the store's own
  `MaxMessageBytes` (decision 6's "second line of defence"), and `StoreFull` when a new mailbox
  would need a `UIDVALIDITY` past 2106; decision 6 can say so.
- BL-190 could not edit the ADR: BL-154 held `Documentation/Planning/Decisions` at the time.

## Acceptance criteria

- [ ] ADR-0050 decision 6's table names `MailboxStore.DefaultMaxMessages`,
      `MailboxStore.DefaultMaxTotalMessageBytes`, `MailboxStore.DefaultMaxMailboxes` and
      `MailboxStore.MaxMailboxNameBytes`, with a sentence saying why the type is not `MailStore`.
- [ ] ADR-0050 decision 6 names the `MessageTooLarge` outcome for a message past the store's own
      bound.
- [ ] `git grep -n "MailStore\.Default\|MailStore\.MaxMailbox" -- Documentation` finds nothing.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
