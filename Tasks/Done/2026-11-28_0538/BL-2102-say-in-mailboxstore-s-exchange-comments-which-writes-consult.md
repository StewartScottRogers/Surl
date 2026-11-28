---
id: BL-2102
title: Say in MailboxStore's exchange comments which writes consult the out-of-space fault point
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary]
requirement: none
created: 2026-11-28
completed: 2026-11-28
---
# BL-2102 — Say in MailboxStore's exchange comments which writes consult the out-of-space fault point

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

- [x] No `exchange` parameter comment on a method that never consults the fault plan mentions the out-of-space fault point.
- [x] Each such comment says the exchange supplies the `ExchangeId` of the events the method publishes.
- [x] The comments on the delivery and append methods that consult the fault plan still say so.
- [x] `dotnet build` is clean and the diff changes only XML comments (and this task's own file).

## Notes

- Checked against the code: `IsOutOfSpace` and `IsOutOfSpaceAsync` are reached only from the `Deliver`,
  `DeliverAsync`, `Append` and `AppendAsync` paths. `ChangeFlags`, both `Expunge` overloads, `Copy`, `Move`,
  `CreateMailbox`, `DeleteMailbox` and `RenameMailbox` reach neither; each comment now names the events
  its method publishes with the exchange's `ExchangeId`.
- The consulting methods' comments said the exchange is null "for a seed or other write with no exchange".
  `Seed` never calls them and skips the fault plan, so they now say "for a write with no exchange, which still
  asks the fault plan". The comment on `DeliverAsync(copies)` now refers to the single-message `Deliver`, as its
  synchronous twin does.
- Verify: `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes` clean, fast tests
  30812 passed, 0 failed, 3 skipped. The only file changed besides task files is `MailboxStore.cs`, XML comments.
- Follow-up filed: BL-2108 (incomplete `<returns>` lists and the project `CLAUDE.md`'s event statement).

## Log

- 2026-11-28: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. MailboxStore's exchange comments name the out-of-space fault point only on the delivery and append methods that consult it
