---
id: BL-1490
title: Deliver a copy per owner with Delivered-To lines through the mail store
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1487, BL-1489]
touches: [Surl.MailStore.UnitLibrary, Surl.MailStore.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1490 — Deliver a copy per owner with Delivered-To lines through the mail store

## Goal

`MailboxStore` can deliver one separate pending message per owner as a single all-or-none batch, and every delivery event names the envelope addresses that reached that owner in `DeliveredFor`.

## Context

ADR-0053 Amendment 1 decisions 3, 4, 5 and 7 (the amendment is in `Documentation/Planning/Decisions/ADR-0053-how-the-smtp-server-answers-upstream-curl.md`), on top of ADR-0050 decision 3 (all-or-none) and `Protocols/Mail/Surl.MailStore.UnitLibrary/MailboxStore.cs` (`Deliver`, `DeliverAsync`, `DeliverPending`, `CreatePendingMessage`). Today `Deliver` takes one `PendingMessage` for every owner (one shared file). Add an overload of `Deliver` and `DeliverAsync` that takes the owners' copies as a list of (recipient owner, its own `PendingMessage`, its `DeliveredFor` addresses) and stores them all or none, with the same refusals (`MessageTooLarge`, `StorageFailed`, `StoreFull`, `QuotaExceeded`, the out-of-space fault) and disposing every pending message whatever the outcome. Every `StoreChangedEvent` with `StoreChange.Delivered` carries `DeliveredFor` (from BL-1489); the existing single-message overloads fill it with the addresses the `MailRecipient` was looked up for when it records them, else leave it null. Also add `MailboxStore.DeliveredToEnabled` (init-only bool, default false) for the composition root to set (BL-1492) and the SMTP server to read (BL-1491). The index format is unchanged. Update the project `CLAUDE.md`.

## Acceptance criteria

- [ ] The new `Deliver` and `DeliverAsync` overloads exist; a test delivers two owners' differing copies and each owner's `INBOX` holds only its own bytes.
- [ ] A test shows one owner's refusal (`QuotaExceeded`, then `StoreFull`, then `MessageTooLarge`) stores nothing for any owner and deletes every pending file.
- [ ] Each delivery `StoreChangedEvent` carries that owner's `DeliveredFor` addresses in the order given; a test pins it, and one pins that the pre-existing overloads publish what they did before.
- [ ] `MailboxStore.DeliveredToEnabled` exists, defaults to false and is documented; a test pins the default.
- [ ] `dotnet build Protocols/Mail/Surl.MailStore.UnitLibrary -warnaserror` is clean; `Surl.MailStore.UnitTests` fast tests pass at 100% line and branch coverage for the new code, no `Integration` test needed.
- [ ] `Protocols/Mail/Surl.MailStore.UnitLibrary/CLAUDE.md` describes the per-owner delivery and `DeliveredFor`.

## Notes

## Log

- 2026-11-20: Created.
