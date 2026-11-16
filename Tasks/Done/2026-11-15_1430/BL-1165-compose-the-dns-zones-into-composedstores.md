---
id: BL-1165
title: Compose the DNS zones into ComposedStores
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1158, BL-1164]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-061
created: 2026-11-14
completed: 2026-11-15
---
# BL-1165 — Compose the DNS zones into ComposedStores

## Goal

The composed instance's DNS zone set is registered in `ComposedStores`, so `ComposedServing.Stores` seeds a zone and a whole-instance reset, snapshot and restore include the zones.

## Context

- `Application/Surl.Composition.UnitLibrary/ServiceState.cs` holds `DnsZones` (a `DnsZoneSet`), loaded by `SurlServerComposition.LoadDnsZonesAsync`, and `ComposedProtocolServices.DnsZones` hands it to every DNS registration in `ExtendedProtocolServers.cs`. Register it through BL-1158's seam with BL-1164's operations, in the place BL-1151's ADR gives it, applying the ADR's rule for zones loaded from `--directory`.
- `ComposedStores` gains the zone seed member the ADR names.

## Acceptance criteria

- [x] Named tests: `ComposedStores` seeds a zone that a `dns://` UDP exchange of the same instance then answers.
- [x] Named tests: a whole-instance reset removes the seeded zone; snapshot then restore brings it back; zones loaded from `--directory` behave as the ADR decides.
- [x] Every existing `Surl.Composition.UnitTests` test passes unchanged; `dotnet build Surl.slnx -warnaserror` is clean and the fast tests are green.
- [x] `Surl.Composition.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Registered only when a dns server is composed (never with SURL_EXTENSIONS=0), like Redis. Order: Zones comes last, after the broker stores. ADR-0158 says keyspace, directory, zones, but the exchanges (BL-1160 line) already sit after the directory and their tests pin that order, so Zones is appended, which keeps ADR-0158 decision 5's rule that the existing order never changes. Three order-pinning tests (Amqp0, Redis) gain Zones at the end.
- Zones loaded from --directory are in-memory copies (ADR-0158 decision 6): reset returns to them via DnsZoneSet.Add's configured view; no file is written.
- LoadDnsZonesAsync gained an optional events parameter so the set publishes on the instance's stream.
- The query test uses DNS over TCP: the fake listener feeds connections only; both transports read the same DnsZoneSet.

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. ComposedStores seeds DNS zones (SeedZoneAsync); whole-instance reset, snapshot and restore include the zones
