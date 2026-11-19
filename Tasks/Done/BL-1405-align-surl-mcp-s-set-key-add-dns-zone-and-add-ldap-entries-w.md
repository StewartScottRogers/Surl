---
id: BL-1405
title: Align surl-mcp's set_key, add_dns_zone and add_ldap_entries with BL-1404's amendment to ADR-0158
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1404, BL-1410]
touches: [Application/Surl.Mcp.UnitLibrary, Application/Surl.Mcp.UnitTests]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-1405 — Align surl-mcp's set_key, add_dns_zone and add_ldap_entries with BL-1404's amendment to ADR-0158

## Goal

`surl-mcp`'s `set_key` answers the key in the shape BL-1404's amendment to ADR-0158 states (one shape with
`read_key` and `list_keys`), and `set_key`, `add_dns_zone` and `add_ldap_entries` on a server with no listen URL for
their store answer an `isError` result with the text the amendment states.

## Context

- Surly cookbook bug report 2026-11-19 09:30, finding 7; review
  `Z:\repos\Surly\Book\parts\05-new-protocols\chapters\04-lookup-and-data\recipes\redis-seed-keys-from-an-agent\review.md`
  finding 4.
- `set_key`'s result: `Application/Surl.Mcp.UnitLibrary/SetKeyTool.cs:68-72` writes `"key"` from `NameOf`
  (lines 77-98, the keyspace event name `<database>/<key>`, per ADR-0158 decision 9 as first written).
  `read_key`/`list_keys` write `key` or `key_base64` through `ReadBackJson.WriteBytes`
  (`ReadKeyTool.WriteInfo`, `ReadKeyTool.cs:49-58`). Change `SetKeyTool` to the amended shape; reuse
  `ReadBackJson.WriteBytes` for the key if the amendment says so, and drop `NameOf` if nothing else uses it.
- The no-listener refusal itself comes from the composition (BL-1410): `SurlServer.Keys.SetAsync`,
  `Zones.AddZoneAsync` and `Directory.AddEntriesAsync` throw `SurlSeedRefusedException`, which
  `RunningServerTool` (`Application/Surl.Mcp.UnitLibrary/RunningServerTool.cs:58`, `catch (InvalidOperationException)`)
  already turns into an `isError` result. This task pins the text an agent sees and adjusts it only if the
  amendment names a different one.
- Tests: `Application/Surl.Mcp.UnitTests/LookupToolsTests.cs` (start `redis://`, `ldap://`, `dns://` hosts) and
  `StoreToolsTests.cs`; `McpTestHost` builds every tool.
- Stand-in: an agent seeding a key reads back the same name `read_key` and `list_keys` use, and seeding a store its
  server does not serve fails at once with the store named; no event changes (the `StoreChangedEvent` name stays
  ADR-0158 decision 2's).

## Acceptance criteria

- [x] A failing test first: a `LookupToolsTests` test calls `set_key` with `{"key": "greeting", "value": "hi"}` on a
      `redis://` server and pins the amended result exactly (for example `"key": "greeting"` with `database` 0, as
      the amendment states), the same key text `list_keys` then answers.
- [x] A test pins `set_key`'s result for a non-UTF-8 key given as `key_base64`, per the amendment.
- [x] Tests on an `http://`-only server pin the `isError` text of `set_key`, `add_dns_zone` and `add_ldap_entries`.
- [x] `SetKeyTool`'s XML doc comment and `Description` say what the result now holds.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and `Surl.Mcp.UnitLibrary` stays at 100% line
      and branch coverage.

## Notes

- BL-1396 also changes `Surl.Mcp.UnitLibrary`; the board will not run the two at once.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing.
- 2026-11-19: set_key now answers key/key_base64 (ReadBackJson.WriteBytes), database, event_name, byte_count per ADR-0158 amendment 1; NameOf kept for event_name. No-listener refusals pinned with the composition's existing text (store prefix + "This instance holds no ..."); amendment names no different text, so unchanged. Fast tests 375 green.
- 2026-11-19: Doing -> Done. set_key answers key/key_base64, database, event_name; seeds on unserved stores are pinned isError
