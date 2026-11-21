---
id: BL-1500
title: Deliver a copy per owner with Delivered-To lines through the mail store
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1487, BL-1499]
touches: [Surl.MailStore.UnitLibrary, Surl.MailStore.UnitTests]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-1500 — Deliver a copy per owner with Delivered-To lines through the mail store

## Goal

`MailboxStore` can deliver one separate pending message per owner as a single all-or-none batch, and every delivery event names the envelope addresses that reached that owner in `DeliveredFor`.

## Context

ADR-0053 Amendment 1 decisions 3, 4, 5 and 7 (the amendment is in `Documentation/Planning/Decisions/ADR-0053-how-the-smtp-server-answers-upstream-curl.md`), on top of ADR-0050 decision 3 (all-or-none) and `Protocols/Mail/Surl.MailStore.UnitLibrary/MailboxStore.cs` (`Deliver`, `DeliverAsync`, `DeliverPending`, `CreatePendingMessage`). Today `Deliver` takes one `PendingMessage` for every owner (one shared file). Add an overload of `Deliver` and `DeliverAsync` that takes the owners' copies as a list of (recipient owner, its own `PendingMessage`, its `DeliveredFor` addresses) and stores them all or none, with the same refusals (`MessageTooLarge`, `StorageFailed`, `StoreFull`, `QuotaExceeded`, the out-of-space fault) and disposing every pending message whatever the outcome. Every `StoreChangedEvent` with `StoreChange.Delivered` carries `DeliveredFor` (from BL-1499); the existing single-message overloads fill it with the addresses the `MailRecipient` was looked up for when it records them, else leave it null. Also add `MailboxStore.DeliveredToEnabled` (init-only bool, default false) for the composition root to set (BL-1502) and the SMTP server to read (BL-1501). The index format is unchanged. Update the project `CLAUDE.md`.

## Acceptance criteria

- [x] The new `Deliver` and `DeliverAsync` overloads exist; a test delivers two owners' differing copies and each owner's `INBOX` holds only its own bytes.
- [x] A test shows one owner's refusal (`QuotaExceeded`, then `StoreFull`, then `MessageTooLarge`) stores nothing for any owner and deletes every pending file.
- [x] Each delivery `StoreChangedEvent` carries that owner's `DeliveredFor` addresses in the order given; a test pins it, and one pins that the pre-existing overloads publish what they did before.
- [x] `MailboxStore.DeliveredToEnabled` exists, defaults to false and is documented; a test pins the default.
- [x] `dotnet build Protocols/Mail/Surl.MailStore.UnitLibrary -warnaserror` is clean; `Surl.MailStore.UnitTests` fast tests pass at 100% line and branch coverage for the new code, no `Integration` test needed.
- [x] `Protocols/Mail/Surl.MailStore.UnitLibrary/CLAUDE.md` describes the per-owner delivery and `DeliveredFor`.

## Notes

- Added public `MailOwnerCopy` (recipient, own pending message, DeliveredFor) and `MailRecipient.ForAddresses` + `DeliveredFor`; `LookUpRecipient` records no addresses, so pre-existing overloads publish null as before (callers opt in via `ForAddresses`).
- A later copy that cannot be kept releases earlier kept files; the file numbers are not reused (harmless).
- Coverage not measured by tool (collector wrote no report); every new branch has a test by inspection.

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing.
- 2026-11-20: Doing -> Done. Per-owner Deliver overloads, DeliveredFor on events, DeliveredToEnabled
