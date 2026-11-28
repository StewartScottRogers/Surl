---
id: BL-2032
title: Take redis_keys, directory_ldif and dns_zones in surl-mcp's start_server
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2031]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests, Documentation/Planning/Decisions/ADR-0158-how-the-redis-keyspace-the-ldap-directory-and-the-dns-zones-are-seeded-reset-snapshotted-restored-and-observed.md, Documentation/Planning/Decisions/README.md]
requirement: FR-061
created: 2026-11-27
completed: 2026-11-28
---
# BL-2032 — Take redis_keys, directory_ldif and dns_zones in surl-mcp's start_server

## Goal

An agent seeds Redis keys, LDAP entries and DNS zones in the `start_server` call itself, and `reset_server` returns the server to them.

## Context

- ADR-0158 decisions 3 and 9 (each start property has the matching seed tool's shape) and Amendment 1 (A2). The library is `Application/Surl.Mcp.UnitLibrary`, the tool `StartServerTool.cs`; fill `SurlServerOptions.RedisKeys`, `DirectoryLdif` and `DnsZones` (BL-2031).
- Shapes: `redis_keys` is an array of objects with `key` or `key_base64`, `value` or `value_base64`, `database` (0 to 15, default 0) and `expire_ms` (1 to 31536000000), as `SetKeyTool`; `directory_ldif` is a string, as `add_ldap_entries`' `ldif`; `dns_zones` is an array of objects with required `apex` and `zone_text`, as `AddDnsZoneTool`. Nested objects set `additionalProperties: false`; `McpToolInputSchema` checks `items`, `properties`, `required`, `minimum` and `maximum`. No `maxItems` is needed: the 4 MiB message limit bounds a call.
- Reuse `SeededContent.TryRead` for the either-text-or-base64 rule and the 16 MiB bound, and refuse an empty key as `set_key` does. A bad item is `isError` naming it (`redis_keys[1]: ...`), with nothing started.
- A seed the store refuses reaches the tool as `SurlSeedRefusedException`: answer `isError: true` with its text, with no server left running and no slot of `MaxServers` used. No value is echoed in a result or a log, and no local path is read.
- Update the tool's description, `ResetServerTool`'s description (it names only files, mail, messages and accounts) to include the three stores and start-time seeds, and the library's `CLAUDE.md`. The `README.md` pages are BL-2034.
- Stand-in: seeds the three shared stores at start; each publishes `Seeded`, seen by `get_events`; no fault rule is consulted.

## Acceptance criteria

- [x] `StartServerTool.InputSchema` declares the three optional properties with the shapes above, and the tool and `reset_server` descriptions name them or what reset returns to.
- [x] Named test in `Surl.Mcp.UnitTests`: a `start_server` call with all three answers a server on which `read_key`, `read_directory` and `read_zone` return the seeds.
- [x] Named test: after `set_key`, `add_ldap_entries` and `add_dns_zone` with further items, `reset_server` leaves exactly the start-time ones.
- [x] Named tests: a refused seed, and a seed for a store no listen URL serves, are `isError: true` with the store's words, and `list_servers` then lists no server.
- [x] Named tests: an item with both or neither of `key` and `key_base64` (and likewise for the value), an empty key and a bad base64 string are each `isError` naming the item, and an unknown nested property is the schema's `-32602` naming the item (ADR-0158 amendment 5; see Notes), with the value absent from every such answer.
- [x] `dotnet build Application/Surl.Mcp.UnitTests -warnaserror` is clean, the fast tests pass without `TestCategory=Integration`, and `Surl.Mcp.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Delivered in the lane session (plan, tests first, implementation, verify), with `code-reviewer` as the one
  delegated stage. No `conformance-auditor` run: upstream curl does not speak MCP, and no byte a Surl listener
  sends changed.
- Tests: `StartServerSeedTests`, and `ProgramTests.RunAsync_StartServerWithASeedItemItsSchemaRefuses_NamesTheItemAndNeverItsValue`.
- Code: `StartTimeSeedArguments` reads the three properties; `RedisKeyArguments` reads one key for both
  `set_key` and `redis_keys`.
- Decided (ADR-0158 amendment 5): criterion 5 first said an unknown nested property is `isError`. Context asks for
  `additionalProperties: false`, and the MCP core runs the schema check before the tool (ADR-0103), so the answer a
  client gets is `-32602` naming `arguments.redis_keys[1].ttl`. A second check in the tool would never be seen, so the
  criterion's wording was corrected instead.
- The nested `key` has no `minLength` (unlike `set_key`'s), so an empty key is answered naming its item.
- "No value is echoed" holds for Redis values and for every schema and item refusal. A store's refusal of
  `directory_ldif` or a zone is passed on in its own words, as `add_ldap_entries` and `add_dns_zone` pass it, and
  may quote part of the text; the description and the ADR say so.
- Review found `database: 1.0` and `expire_ms: 6e4` answered `-32603` (already so in `set_key`); fixed in
  `RedisKeyArguments`. The other tools' integer arguments are BL-2054.
- Added one test row set to `StartServerSafetyCheckTests`: `IsConfigFileOption` was at 50% branch coverage before
  this task, which criterion 6 does not allow.
- `touches` gained ADR-0158 and the ADR index (amendment 5); no task in Doing names either.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-28: Doing -> Done. start_server takes redis_keys, directory_ldif and dns_zones, and reset_server returns the server to them
