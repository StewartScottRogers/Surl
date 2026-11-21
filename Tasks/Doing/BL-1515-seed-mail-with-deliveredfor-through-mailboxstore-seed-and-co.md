---
id: BL-1515
title: Seed mail with DeliveredFor through MailboxStore.Seed and ComposedStores.SeedMailAsync
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1502, BL-1507]
touches: [Protocols/Mail/Surl.MailStore.UnitLibrary, Protocols/Mail/Surl.MailStore.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1515 — Seed mail with DeliveredFor through MailboxStore.Seed and ComposedStores.SeedMailAsync

## Goal

A seeded mail delivery (`MailboxStore.Seed`, `ComposedStores.SeedMailAsync`) publishes its `StoreChangedEvent` with `DeliveredFor` set to the one recipient address handed in, so `Surl.Api` and `Surl.Mcp` seeds are observable (ADR-0053 Amendment 1).

## Context

`MailboxStore.Seed` (`Protocols/Mail/Surl.MailStore.UnitLibrary/MailboxStore.Control.cs`, `SeedTo`) delivers with `new OwnerDelivery(owner, null)`, so every seeded event has `DeliveredFor` null. BL-1500 added `DeliveredFor` and `MailOwnerCopy`/`MailRecipient.ForAddresses`. Seeding is the path BL-1503 (`Surl.Api` `Mail.DeliverAsync`), BL-1504 (`Surl.Mcp`) and BL-1495 (control endpoint) use, and each asserts `DeliveredFor` on the event stream. Waits for BL-1507 (MailStore) and BL-1502 (Composition), which hold these projects now. Optionally let `ComposedStores` expose whether `--mail-delivered-to` is on (`MailboxStore.DeliveredToEnabled`, set by BL-1502) so hosts can read it instead of copying the option.

## Acceptance criteria

- [ ] `MailboxStore.Seed` publishes `DeliveredFor` holding the one `recipientPath` address (brackets removed) on each seeded copy's event; a test pins it, and one pins that bytes stored are unchanged.
- [ ] `ComposedStores.SeedMailAsync` carries it through, pinned by a `ComposedStores` test reading the event stream.
- [ ] `ComposedStores` (or the mail seed result) exposes whether `DeliveredToEnabled` is on, pinned by a test.
- [ ] The two projects' `CLAUDE.md` files describe it.
- [ ] `dotnet build` is clean and the fast tests of both test projects pass.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-21: Backlog -> Doing.
