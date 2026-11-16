---
id: BL-1220
title: Read the DNS zones back through ComposedStores, Surl.Api, Surl.Mcp and the control endpoint (ADR-0164 decision 9)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1194]
touches: [Surl.Extended.Protocol.DnsQuery.UnitLibrary, Surl.Extended.Protocol.DnsQuery.UnitTests, Surl.Composition.UnitLibrary, Surl.Composition.UnitTests, Surl.Api, Surl.Api.UnitTests, Surl.Mcp, Surl.Mcp.UnitLibrary, Surl.Mcp.UnitTests, Surl.Control.UnitLibrary, Surl.Control.UnitTests]
requirement: none
created: 2026-11-15
completed: 2026-11-15
---
# BL-1220 — Read the DNS zones back through ComposedStores, Surl.Api, Surl.Mcp and the control endpoint (ADR-0164 decision 9)

## Goal

A test reads the DNS zones back, one apex or all, through all three hosts.

## Context

ADR-0164 decision 9 (decisions 2 to 4 apply unchanged); the store is seeded by ADR-0158. Set `touches` at claim time to the projects that hold the store, `Surl.Composition`, `Surl.Api`, `Surl.Mcp.UnitLibrary` and `Surl.Control.UnitLibrary`, or split the task if the board says they overlap.

## Acceptance criteria

- [x] The store is readable through `ComposedStores`, `Surl.Api`, `Surl.Mcp` and the control endpoint with the member, tool and route names ADR-0164 decision 9 gives; a read publishes no event, consults no fault and returns no secret.
- [x] Unit tests hold every touched library at the quality gates; `--aihelp` and docs name the new reads.

## Notes

- Decided (ADR-0164 decision 9 gives the names): `Zones.ListAsync()` returns the apexes (final dot, order added); `Zones.ReadAsync(apex)` returns master-file text written by the new `DnsZoneFileWriter` (absolute names, TTL, class IN, RFC 3597 form for types without field syntax), which seeds an identical zone. `read_zone` takes an optional `apex`: with it `{apex, zone}`, without it `{zones: [...]}` (one apex or all). Routes `GET /v1/zones` (JSON) and `GET /v1/zones/{apex}` (`text/dns`, RFC 4027). No aihelp change: the reads are API, MCP and control-endpoint surface, not command-line options.
- Added the DnsQuery library and tests to `touches` (the zone writer lives with the zone set); no task in Doing named them.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. DNS zones read back as zone-file text through ComposedStores, Surl.Api Zones.ReadAsync/ListAsync, read_zone and GET /v1/zones
