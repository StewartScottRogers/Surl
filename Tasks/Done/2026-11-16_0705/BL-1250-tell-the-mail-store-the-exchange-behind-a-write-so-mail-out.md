---
id: BL-1250
title: Tell the mail store the exchange behind a write so mail out-of-space rules match listener and scheme and mail changes name their exchange (ADR-0168)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1239]
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests]
requirement: FR-062
created: 2026-11-16
completed: 2026-11-16
---
# BL-1250 — Tell the mail store the exchange behind a write so mail out-of-space rules match listener and scheme and mail changes name their exchange (ADR-0168)

## Goal

MailboxStore mutators take an optional trailing ExchangeContext; a supplied exchange fills the out-of-space fault point and the ExchangeId of every published event.

## Context

ADR-0168 decisions 2 to 4 (Documentation/Planning/Decisions/ADR-0168-how-the-content-store-and-the-mail-store-learn-the-exchange-behind-a-write.md). Start at MailboxStore.cs (Deliver, DeliverAsync, Append, AppendAsync and the other publishing mutators) and MailboxStore.Events.cs. Copy MessageBus.Events.cs line ~96. No wire byte changes.

## Acceptance criteria

- [x] Every publishing mutator of MailboxStore has a trailing `ExchangeContext? exchange = null`; existing callers compile unchanged.
- [x] A unit test proves `out-of-space;listener=...` and `out-of-space;scheme=smtp` fire for a Deliver and an Append given an exchange, and a seed without one still publishes ExchangeId null.
- [x] A unit test proves PublishMessage, PublishMailbox and both FaultAppliedEvents carry the exchange's ID.
- [x] 100% line and branch coverage held; build clean; fast tests green.

## Notes

All publishing mutators (Deliver, DeliverAsync, Append x2, AppendAsync, ChangeFlags, Expunge x2, Copy, Move, Create/Delete/RenameMailbox, MaildropLock.RemoveMessages) take a trailing optional `ExchangeContext? exchange = null`. Listen URL on the fault point is `ReportedListenUrl.Format(exchange.ListenUrl)`, as the servers do. Seeds pass null. Tests: MailboxStoreExchangeTests.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing.
- 2026-11-16: Doing -> Done. MailboxStore mutators take an optional exchange: out-of-space rules match listener and scheme, events carry ExchangeId
