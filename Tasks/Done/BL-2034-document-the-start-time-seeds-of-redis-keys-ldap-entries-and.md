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
completed: 2026-11-28
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

- [x] The Surl.Api README names `SurlServerOptions.RedisKeys`, `DirectoryLdif` and `DnsZones` with an example, says they are applied before any listener binds, that `ResetAsync` returns to them, and that a refused one throws `SurlSeedRefusedException` from `StartAsync`.
- [x] The surl-mcp README names `redis_keys`, `directory_ldif` and `dns_zones` with their item shapes, the `isError` answer for a refused seed, and that `reset_server` returns to them.
- [x] The wiki page states the same for both hosts, that a seed made while serving is dropped by a reset, and cites the BL-2033 recipe test by name.
- [x] FR-061 mentions the start-time seeds.
- [x] Only the four files in `touches` change.

## Notes

- Written by `align-and-document`, claims checked against source. The recipe test is
  `StandInLdapDirectoryRecipeTests.ResetAsync_AfterStartTimeAndRuntimeSeeds_SearchStillFindsOnlyTheStartTimeEntry`.
- Also corrected beside the new text: a seed throws when no listen URL serves the store (not when no server "is
  composed"); only a bad database or expiry is `ArgumentOutOfRangeException`; a reset does not free the apex of a
  start-time zone.
- Verified by running: the Surl.Api README's start-time example (temporary file-based app) printed
  `greeting=hello` after the reset, and the surl-mcp README's JSON started all three listeners through
  `surl-mcp`; a Redis seed with only an LDAP URL was refused (`SurlSeedRefusedException`, `isError: true`).
- `dotnet build` clean (0 warnings) and the fast tests passed. No `.cs` or project file changed.
- "Only the four files" holds for the delivery; the board additionally gained the follow-ups BL-2066 (XML docs),
  BL-2067 (Glossary "directory file" row) and BL-2068 (FR-061 "one at a time" versus the hosts).

## Log

- 2026-11-27: Created.
- 2026-11-28: Backlog -> Doing.
- 2026-11-28: Doing -> Done. The Surl.Api, surl-mcp and stand-in pages and FR-061 document the start-time seeds of Redis keys, LDAP entries and DNS zones and that a reset returns to them
