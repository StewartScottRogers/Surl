---
id: BL-1164
title: Seed, reset, snapshot and restore the DNS zones and publish their changes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1157]
touches: [Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary, Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitTests]
requirement: FR-061
created: 2026-11-14
completed: 2026-11-14
---
# BL-1164 — Seed, reset, snapshot and restore the DNS zones and publish their changes

## Goal

The DNS zone set can be seeded with a zone while the server runs, reset to the zones it held at start, snapshotted and restored, and publishes a `StoreChangedEvent` for each, so an in-memory DNS stand-in can answer the names a test needs.

## Context

- `Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary/DnsZoneSet.cs` has `Add(string fileName, byte[] content)` (zone-file text, `ZoneFileExtension` `.zone`), `Answer(DnsQuestion)`, and limits `DefaultMaxZones`, `DefaultMaxRecords`, `DefaultMaxTotalBytes`. It is loaded at start by `SurlServerComposition.LoadDnsZonesAsync` from the data directory and is otherwise empty. The UDP, TCP, DoT/DoH/DoQ servers all read the one set.
- Queries answered while the set changes must see either the old or the new zones. The seed input, events (`StoreKind` from BL-1157), bounds and the on-disk rule are BL-1151's ADR. A seed never consults the fault plan.
- Judged by RFC 1035 and the DNS specifications (ADR-0083, ADR-0092).

## Acceptance criteria

- [x] Named tests: a zone seeded from zone-file text is answered authoritatively for its names; a seed that fails to parse or exceeds a limit is refused with the parser's message and changes nothing; seeding a zone whose apex exists behaves as the ADR decides.
- [x] Named tests: reset returns to the zones the set was built with; snapshot, change, restore gives back the snapshot's zones; a snapshot from another set is refused.
- [x] Named tests: seed, reset and restore each publish the `StoreChangedEvent` the ADR decides.
- [x] Every existing DNS test passes unchanged; `dotnet build -warnaserror` is clean and the fast tests are green.
- [x] `Surl.Extended.Protocol.DnsQuery.UnitLibrary` keeps 100% line and branch coverage with no method over complexity 10.

## Notes

- Decided under ADR-0158: `SeedZone(apex, text, keepOnReset)` reads as `Add(apex + ".zone")`, max 16 MiB, refused with `DnsZoneFileException`; an existing apex (live or configured) is "a second zone". `Add` loads configured zones, which `Reset` returns to; a seed is dropped by reset unless `keepOnReset`. Events: Seeded per zone (apex, zone bytes), Reset (empty name, 0), Restored (empty name, snapshot bytes).
- Queries take an immutable `DnsZoneView`, so they see old or new zones, never a mix. Host wiring (Api, Mcp, control) is other tasks.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Done. DNS zones can be seeded, reset, snapshotted and restored, publishing StoreChangedEvent for each
