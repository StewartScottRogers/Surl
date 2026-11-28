---
id: BL-2021
title: Take redis_keys, directory_ldif and dns_zones in surl-mcp's start_server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2020]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: FR-061
created: 2026-11-27
completed:
---
# BL-2021 — Take redis_keys, directory_ldif and dns_zones in surl-mcp's start_server

## Goal

An agent seeds Redis keys, LDAP entries and DNS zones in the `start_server` call itself, and `reset_server` returns the server to them.

## Context

- ADR-0158 decisions 3 and 9 (each start property has the matching seed tool's shape) and Amendment 1 (A2). The library is `Application/Surl.Mcp.UnitLibrary`, the tool `StartServerTool.cs`; fill `SurlServerOptions.RedisKeys`, `DirectoryLdif` and `DnsZones` (BL-2020).
- Shapes: `redis_keys` is an array of objects with `key` or `key_base64`, `value` or `value_base64`, `database` (0 to 15, default 0) and `expire_ms` (1 to 31536000000), as `SetKeyTool`; `directory_ldif` is a string, as `add_ldap_entries`' `ldif`; `dns_zones` is an array of objects with required `apex` and `zone_text`, as `AddDnsZoneTool`. Nested objects set `additionalProperties: false`; `McpToolInputSchema` checks `items`, `properties`, `required`, `minimum` and `maximum`. No `maxItems` is needed: the 4 MiB message limit bounds a call.
- Reuse `SeededContent.TryRead` for the either-text-or-base64 rule and the 16 MiB bound, and refuse an empty key as `set_key` does. A bad item is `isError` naming it (`redis_keys[1]: ...`), with nothing started.
- A seed the store refuses reaches the tool as `SurlSeedRefusedException`: answer `isError: true` with its text, with no server left running and no slot of `MaxServers` used. No value is echoed in a result or a log, and no local path is read.
- Update the tool's description, `ResetServerTool`'s description (it names only files, mail, messages and accounts) to include the three stores and start-time seeds, and the library's `CLAUDE.md`. The `README.md` pages are BL-2023.
- Stand-in: seeds the three shared stores at start; each publishes `Seeded`, seen by `get_events`; no fault rule is consulted.

## Acceptance criteria

- [ ] `StartServerTool.InputSchema` declares the three optional properties with the shapes above, and the tool and `reset_server` descriptions name them or what reset returns to.
- [ ] Named test in `Surl.Mcp.UnitTests`: a `start_server` call with all three answers a server on which `read_key`, `read_directory` and `read_zone` return the seeds.
- [ ] Named test: after `set_key`, `add_ldap_entries` and `add_dns_zone` with further items, `reset_server` leaves exactly the start-time ones.
- [ ] Named tests: a refused seed, and a seed for a store no listen URL serves, are `isError: true` with the store's words, and `list_servers` then lists no server.
- [ ] Named tests: an item with both or neither of `key` and `key_base64` (and likewise for the value), an empty key, a bad base64 string and an unknown nested property are each `isError` naming the item, with the value absent from the result.
- [ ] `dotnet build Application/Surl.Mcp.UnitTests -warnaserror` is clean, the fast tests pass without `TestCategory=Integration`, and `Surl.Mcp.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-27: Created.
