---
id: BL-2030
title: Seed Redis keys, LDAP entries and DNS zones that survive a reset through ComposedStores
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2028, BL-2029]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-061
created: 2026-11-27
completed:
---
# BL-2030 — Seed Redis keys, LDAP entries and DNS zones that survive a reset through ComposedStores

## Goal

A host can seed a Redis key, LDAP entries or a DNS zone through `ComposedStores` so that a later reset of that store returns to it.

## Context

- ADR-0158 decisions 3 and 4, and Amendment 1 (A2). `Surl.Api` (BL-2031) applies its start-time seeds through these calls after composing and before serving, so that its event recording, which subscribes after `SurlServerComposition.ComposeAsync` returns, sees the `Seeded` events. So the seeds are not applied inside `ComposeAsync`.
- In `Application/Surl.Composition.UnitLibrary/ComposedStores.cs`, add an overload of each of `SeedKeyAsync`, `SeedEntriesAsync` and `SeedZoneAsync` taking `bool keepOnReset` immediately before the `CancellationToken`. The existing signatures keep their behaviour (`keepOnReset: false`). Pass the flag to `RedisKeyspace.SeedKey` (BL-2028), `LdapProtocolServer.SeedDirectoryEntries` (BL-2029) and `DnsZoneSet.SeedZone`, which already has it.
- An instance serving no such store keeps the same refusal: `InvalidOperationException` with `This instance holds no Redis keyspace.`, `This instance holds no LDAP directory.` or `This instance holds no DNS zones.`.
- With `--directory`, a reset of `StoreKind.Directory` or `StoreKind.Zones` returns to the copy read from the files at start plus the kept seeds, and writes no file (decision 6).
- `ComposedControlTarget` keeps calling the existing signatures: the control endpoint has no start-time seeds. Nothing is added to the command line, so no `--help` or `--aihelp` change.
- Update the XML comments and the unit library's `CLAUDE.md` (the keyspace and DNS zones bullets say a reset drops a seed).
- Stand-in: the three shared stores above. Seeds publish `Seeded`, never consult the fault plan, and reset, snapshot and restore go through the existing registrations.

## Acceptance criteria

- [ ] `ComposedStores` has the three overloads, each documented.
- [ ] Named tests in `RedisKeyspaceStoreTests`, `LdapDirectoryStoreTests` and `DnsZoneStoreTests`: through a composed start, a kept seed survives `ResetAsync` of its store and a seed through the old signature does not.
- [ ] Named tests: with `--directory` over an in-memory `DataDirectoryFileSystem` holding `directory.ldif` and a `.zone` file, resetting the directory and zones leaves the loaded contents plus the kept seeds, and the files are unchanged.
- [ ] Named test (in or beside `ServedStoresTests`): each new overload on an instance not serving the store throws `InvalidOperationException` with the text above.
- [ ] `dotnet build Application/Surl.Composition.UnitTests -warnaserror` is clean, the fast tests pass without `TestCategory=Integration`, and `Surl.Composition.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-27: Created.
