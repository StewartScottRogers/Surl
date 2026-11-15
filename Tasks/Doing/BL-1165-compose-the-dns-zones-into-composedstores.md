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
completed:
---
# BL-1165 — Compose the DNS zones into ComposedStores

## Goal

The composed instance's DNS zone set is registered in `ComposedStores`, so `ComposedServing.Stores` seeds a zone and a whole-instance reset, snapshot and restore include the zones.

## Context

- `Application/Surl.Composition.UnitLibrary/ServiceState.cs` holds `DnsZones` (a `DnsZoneSet`), loaded by `SurlServerComposition.LoadDnsZonesAsync`, and `ComposedProtocolServices.DnsZones` hands it to every DNS registration in `ExtendedProtocolServers.cs`. Register it through BL-1158's seam with BL-1164's operations, in the place BL-1151's ADR gives it, applying the ADR's rule for zones loaded from `--directory`.
- `ComposedStores` gains the zone seed member the ADR names.

## Acceptance criteria

- [ ] Named tests: `ComposedStores` seeds a zone that a `dns://` UDP exchange of the same instance then answers.
- [ ] Named tests: a whole-instance reset removes the seeded zone; snapshot then restore brings it back; zones loaded from `--directory` behave as the ADR decides.
- [ ] Every existing `Surl.Composition.UnitTests` test passes unchanged; `dotnet build Surl.slnx -warnaserror` is clean and the fast tests are green.
- [ ] `Surl.Composition.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing.
