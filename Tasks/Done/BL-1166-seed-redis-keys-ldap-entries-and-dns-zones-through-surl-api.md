---
id: BL-1166
title: Seed Redis keys, LDAP entries and DNS zones through Surl.Api
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1161, BL-1163, BL-1165]
touches: [Application/Surl.Api, Application/Surl.Api.UnitTests]
requirement: FR-061
created: 2026-11-14
completed: 2026-11-15
---
# BL-1166 — Seed Redis keys, LDAP entries and DNS zones through Surl.Api

## Goal

A test that embeds Surl through `Surl.Api` seeds Redis keys, LDAP entries and DNS zones on a running `SurlServer` with the members BL-1151's ADR names, and `ResetAsync`, `SnapshotAsync` and `RestoreAsync` cover them.

## Context

- Model each surface on the existing ones in `Application/Surl.Api/`: `SurlServerFiles.cs`, `SurlServerMail.cs`, `SurlServerMessages.cs` (thin wrappers over `ComposedServing.Stores`, throwing `SurlSeedRefusedException` with the `StoreKind` when the store refuses). Reset, snapshot and restore already go through `ComposedStores`, so after BL-1161, BL-1163 and BL-1165 they cover the new stores with no change here - prove it with tests.
- About 150 lines of production code: three small classes and three properties on `SurlServer`. Keep each method to one store.
- `SurlEventQuery.Store` filters by `StoreKind`, so a wait for a seeded change works once the kinds exist (BL-1157).

## Acceptance criteria

- [x] `SurlServer` exposes the three seed surfaces BL-1151's ADR names, each with XML doc comments; a refused seed throws `SurlSeedRefusedException` naming its store.
- [x] Named tests in `Application/Surl.Api.UnitTests`: a seeded Redis key, LDAP entry and DNS zone are each seen by the matching store (through the server's composition, in memory, port 0), and each publishes a `StoreChangedEvent` a `WaitForEventAsync` with that `Store` finds.
- [x] Named tests: `ResetAsync` removes all three; `SnapshotAsync` then `RestoreAsync` brings all three back.
- [x] `dotnet build Surl.slnx -warnaserror` is clean, the fast tests are green (no `TestCategory=Integration` needed), and `Surl.Api` keeps 100% line and branch coverage.

## Notes

- Added SurlServerKeys, SurlServerDirectory and SurlServerZones (SurlServer.Keys, .Directory, .Zones) over ComposedStores, per ADR-0158 decision 9. A store refusal (InvalidOperationException; any non-cancel exception for zones, since DnsZoneFileException is not visible to Surl.Api) becomes SurlSeedRefusedException with StoreKind Keyspace, Directory or Zones; argument-range errors stay ArgumentException. Tests: SurlServerTests.Lookups.cs. Start-time options (RedisKeys, DirectoryLdif, DnsZones) are not in this task's criteria and were left out.

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. Surl.Api seeds Redis keys, LDAP entries and DNS zones; reset, snapshot and restore cover them
