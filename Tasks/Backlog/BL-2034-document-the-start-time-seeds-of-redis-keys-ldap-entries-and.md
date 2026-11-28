---
id: BL-2034
title: Document the start-time seeds of Redis keys, LDAP entries and DNS zones in the Surl.Api, surl-mcp and stand-in pages
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-2031, BL-2032, BL-2033]
touches: [Application/Surl.Api/README.md, Application/Surl.Mcp/README.md, Documentation/Wiki/Surl-As-A-Stand-In.md, Documentation/Product/Requirements.md]
requirement: FR-061
created: 2026-11-27
completed:
---
# BL-2034 — Document the start-time seeds of Redis keys, LDAP entries and DNS zones in the Surl.Api, surl-mcp and stand-in pages

## Goal

A reader of the Surl.Api, surl-mcp and stand-in pages learns how to seed the three stores at start and that a reset returns to those seeds.

## Context

- ADR-0158 decisions 3, 4 and 9, Amendment 1 (A2), and its Consequences (the host pages and the wiki name the spellings once built). Code: BL-2028 to BL-2032; recipe test: BL-2033. Document what the code has after them, checked against source.
- `Application/Surl.Api/README.md`: the section introducing `Keys.SetAsync`, `Directory.AddEntriesAsync` and `Zones.AddZoneAsync`, and its reset text. It also says a seed throws when no such server "is composed"; the rule is that no listen URL serves the store.
- `Application/Surl.Mcp/README.md`: `start_server`'s properties and the `reset_server` entry.
- `Documentation/Wiki/Surl-As-A-Stand-In.md`: the seeding, reset and `Surl.Mcp` sections.
- `Documentation/Product/Requirements.md`, FR-061: add that `Surl.Api` and `Surl.Mcp` take start-time seeds for the three stores.
- The command line and the control endpoint have no start-time seeds (`--directory` loads the directory and zones).

## Acceptance criteria

- [ ] The Surl.Api README names `SurlServerOptions.RedisKeys`, `DirectoryLdif` and `DnsZones` with an example, says they are applied before any listener binds, that `ResetAsync` returns to them, and that a refused one throws `SurlSeedRefusedException` from `StartAsync`.
- [ ] The surl-mcp README names `redis_keys`, `directory_ldif` and `dns_zones` with their item shapes, the `isError` answer for a refused seed, and that `reset_server` returns to them.
- [ ] The wiki page states the same for both hosts, that a seed made while serving is dropped by a reset, and cites the BL-2033 recipe test by name.
- [ ] FR-061 mentions the start-time seeds.
- [ ] Only the four files in `touches` change.

## Notes

## Log

- 2026-11-27: Created.
