---
id: BL-1830
title: Say in the Surl.Composition docs that whole-instance reset, snapshot and restore cover every registered store
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Application/Surl.Composition.UnitLibrary]
requirement: none
created: 2026-11-26
completed: 2026-11-27
---
# BL-1830 — Say in the Surl.Composition docs that whole-instance reset, snapshot and restore cover every registered store

## Goal

The Composition library's `CLAUDE.md` and XML doc comments say that whole-instance reset, snapshot and restore
run over every registered store, in `ComposedStores.RegisteredStoreOrder`, with each store in its true place.

## Context

Found while running BL-1822 (checked against the code on 2026-11-26). Prose and XML doc comments only; no
executable line changes. No store, event or fault changes.

- `ComposedStores.ResetAsync` and `TakeSnapshotAsync` walk the private list `controls`, which
  `RegisteredStoreOrder` returns; `RestoreAsync` restores what the snapshot holds in the order taken.
  `StoreOrder` is only the first four: content, mail, bus, accounts.
- The registered order, each store beyond the first four only when composed or served: content, mail, bus,
  accounts, proxy accounts, TLS-SRP accounts, Redis keyspace, LDAP directory, AMQP 0-9-1 exchanges, Kafka
  producers, sessions, DNS zones, uploads (`ComposedStores` constructor, `ComposedStores.JoinTlsSrpAccounts`,
  `SurlServerComposition.Start.cs`; pinned by `Amqp0ExchangeStoreTests` and `DnsZoneStoreTests`).
- `Application/Surl.Composition.UnitLibrary/CLAUDE.md`, bullet "Store control per instance", says the three
  operations "work on every store in `ComposedStores.StoreOrder` (content, mail, bus, accounts)". Its DNS zones
  bullet says `StoreKind.Zones` joins the order "last", but the uploads entry follows it.
- `ComposedStores.cs` says the same wrong thing in the class summary and remarks, the `StoreOrder` summary and the
  summaries of the whole-instance `ResetAsync`, `TakeSnapshotAsync` and `RestoreAsync`; so does the `Stores`
  summary in `ComposedSnapshot.cs`.
- `Amqp0ExchangeStoreRegistration.cs`, class summary, places the exchanges "after the bus and before the accounts'
  place in the order"; they follow the accounts (and the proxy accounts, TLS-SRP accounts, keyspace and directory
  when present).
- BL-1820 also edits the library's `CLAUDE.md` (its opening paragraph only).

## Acceptance criteria

- [x] The "Store control per instance" bullet says whole-instance reset, snapshot and restore cover every store in
      `RegisteredStoreOrder`, that `StoreOrder` is its first four, and that others can follow them.
- [x] The DNS zones bullet places `StoreKind.Zones` before the uploads entry and no longer calls it last.
- [x] No XML doc comment in `ComposedStores.cs` or `ComposedSnapshot.cs` says a whole-instance operation or a
      snapshot covers only `StoreOrder` or only content, mail, bus and accounts.
- [x] The class summary of `Amqp0ExchangeStoreRegistration` places the exchanges after the accounts.
- [x] `dotnet build Application/Surl.Composition.UnitTests -warnaserror` is clean, and the only changed lines in
      `.cs` files are comments.

## Notes

- 2026-11-27: Delivered by `align-and-document`. Also corrected, in the same library, comments on the same subject:
  `ComposedServing.Stores`, the account stores' place in the order, the Redis, DNS and broker-store comments in
  `SurlServerComposition.Start.cs`, one-event-per-store claims (`Sessions` and `Uploads` publish one per server
  kind), which stores a disk-backed instance refuses, and the one-store overloads' exception text.
- 2026-11-27: Verified: `dotnet build -warnaserror` 0 warnings, 0 errors; `dotnet format --verify-no-changes`
  exit 0; fast tests 0 failed, 3 skipped. No changed `.cs` line is other than a comment. Integration tests not
  run: comments and prose only.
- 2026-11-27: Left outside this task: `SurlServer.cs` in `Surl.Api` (BL-1847), the `UploadStoreRegistrations`
  name and summary (BL-1848), the ADRs' order statements (BL-1857, filed from this run).

## Log

- 2026-11-26: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The Surl.Composition CLAUDE.md and XML docs say whole-instance reset, snapshot and restore cover every registered store in RegisteredStoreOrder
