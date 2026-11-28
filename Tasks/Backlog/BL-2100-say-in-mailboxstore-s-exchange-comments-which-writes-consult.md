---
id: BL-2100
title: Say in MailboxStore's exchange comments which writes consult the out-of-space fault point
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary]
requirement: none
created: 2026-11-28
completed:
---
# BL-2100 — Say in MailboxStore's exchange comments which writes consult the out-of-space fault point

## Goal

Each `exchange` parameter comment in `MailboxStore` says what the method does with the exchange, naming the out-of-space fault point only where the method consults it. No behaviour changes.

## Context

Found while working BL-2084 (checked by reading the code on 2026-11-28). In
`Protocols/Mail/Surl.MailStore.UnitLibrary/MailboxStore.cs` the `exchange` parameter of `ChangeFlags`, both `Expunge`
overloads, `Copy`, `Move`, `CreateMailbox`, `DeleteMailbox` and `RenameMailbox` says it "fills the out-of-space fault
point". Only the delivery and append paths call `IsOutOfSpace` or `IsOutOfSpaceAsync`
(`MailboxStore.Events.cs`); for the methods named, the exchange supplies the published event's `ExchangeId`. Check
each method against the code before changing its comment.

Stand-in: a test author reading these comments must not expect an out-of-space rule to refuse a copy, move or
mailbox change that never consults the fault plan.

## Acceptance criteria

- [ ] No `exchange` parameter comment on a method that never consults the fault plan mentions the out-of-space fault point.
- [ ] Each such comment says the exchange supplies the `ExchangeId` of the events the method publishes.
- [ ] The comments on the delivery and append methods that consult the fault plan still say so.
- [ ] `dotnet build` is clean and the diff changes only XML comments (and this task's own file).

## Notes

## Log

- 2026-11-28: Created.
