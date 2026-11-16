---
id: BL-1172
title: Document seeding, resetting and snapshotting the Redis keyspace, the LDAP directory, the DNS zones and broker state
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-1166, BL-1167, BL-1168, BL-1169, BL-1170, BL-1171]
touches: [Documentation/Wiki/Surl-As-A-Stand-In.md, Application/Surl.Api/README.md, Application/Surl.Mcp/README.md, Documentation/Product/Requirements.md]
requirement: FR-061
created: 2026-11-14
completed: 2026-11-15
---
# BL-1172 — Document seeding, resetting and snapshotting the Redis keyspace, the LDAP directory, the DNS zones and broker state

## Goal

The stand-in documentation states, truly and with examples, that the Redis keyspace, the LDAP directory and the DNS zones are seeded, reset, snapshotted and restored through `Surl.Api`, `Surl.Mcp` and the control endpoint, and that a reset also clears the broker state BL-1170 and BL-1171 cover.

## Context

- Built by BL-1157 to BL-1171, decided by BL-1151's and BL-1152's ADRs. No behaviour changes here (`docs` pipeline, `align-and-document`).
- Files: `Documentation/Wiki/Surl-As-A-Stand-In.md` (sections "Composing protocols over the shared stores", "Controlling: seed, reset, snapshot, restore", "The control endpoint", "`Surl.Mcp`, for AI agents"), `Application/Surl.Api/README.md`, `Application/Surl.Mcp/README.md` (tool list), `Documentation/Product/Requirements.md` row FR-061 (which today names only mail, content, message bus and accounts).
- Every example must be one a test runs (the wiki's own rule in its first paragraph): cite BL-1166's and BL-1169's tests.

## Acceptance criteria

- [x] `Documentation/Wiki/Surl-As-A-Stand-In.md` "Controlling" shows a `Surl.Api` seed of a Redis key, an LDAP entry and a DNS zone using the exact member names in `Application/Surl.Api`, and names BL-1169's `StandInLdapDirectoryRecipeTests` as the recipe.
- [x] The wiki's control-endpoint route list and `Surl.Mcp` tool list include the new routes and tools exactly as `ControlEndpoint.Routes` and `tools/list` have them.
- [x] `Application/Surl.Api/README.md` and `Application/Surl.Mcp/README.md` describe the new members and tools with their bounds and refusals.
- [x] `Documentation/Product/Requirements.md` FR-061 names the seven controllable stores (or however many BL-1151 and BL-1152 settled) and ends "Built (<date>): <task IDs>".
- [x] `Documentation/Wiki/Glossary.md` is consulted and every term used matches it; nothing is stated that the code does not do.

## Notes

- Documented only what the code has: `SurlServer.Keys`, `Directory`, `Zones`; control routes `PUT /v1/keys/{database}/{key}`, `POST /v1/directory`, `PUT /v1/zones/{apex}`; MCP tools `set_key`, `add_ldap_entries`, `add_dns_zone`. No Redis recipe test exists, so only BL-1169's LDAP recipe and `SurlServerTests.Lookups` are cited. Start-time seed options are not documented: no such `SurlServerOptions` property exists yet.
- `Application/Surl.Mcp/CLAUDE.md` still lists the older tool set; outside this task's `touches`, left for a later docs task.

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. Wiki, Surl.Api and Surl.Mcp READMEs and FR-061 document seeding, resetting and snapshotting the keyspace, directory, zones and broker state
