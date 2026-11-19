---
id: BL-1404
title: Decide set_key's result key and what the seed tools answer on a server with no listener for their store, amending ADR-0158
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-11-19
completed:
---
# BL-1404 — Decide set_key's result key and what the seed tools answer on a server with no listener for their store, amending ADR-0158

## Goal

An amendment to ADR-0158, "Decided by Claude under Stewart's delegation", settles two things: what `set_key`'s
result names as `key` (today `"0/greeting"`, while `read_key` and `list_keys` answer `"greeting"`), and whether
seeding the Redis keyspace, the DNS zones or the LDAP directory is refused on an instance that has no listen URL
serving that store.

## Context

- Surly cookbook bug report 2026-11-19 09:30, finding 7; review
  `Z:\repos\Surly\Book\parts\05-new-protocols\chapters\04-lookup-and-data\recipes\redis-seed-keys-from-an-agent\review.md`
  finding 4: `set_key` answers `"key": "0/greeting"`, `read_key` and `list_keys` answer `"key": "greeting"`;
  `set_key` and `add_dns_zone` succeed on an HTTP-only server.
- The key name is decided, not a slip: ADR-0158 decision 9's `Surl.Mcp` table gives `set_key` the result
  `{"key": "<Name as decision 2>", "byte_count"}`, and decision 2 makes the keyspace `Name` `<database>/<key>`.
  `Application/Surl.Mcp.UnitLibrary/SetKeyTool.cs:70` and `NameOf` (line 77) do exactly that. ADR-0164 decision 9
  later gave `read_key` and `list_keys` the key as `key` or `key_base64` with no database
  (`ReadKeyTool.WriteInfo`, `ReadKeyTool.cs:49-58`, `ReadBackJson.WriteBytes`). One concept, two names: the
  amendment picks one shape for the tools. Tool results are an interface agents read; the root `CLAUDE.md`'s
  "only grows, never repurposed" rule argues for adding `database` (and `key_base64` for a non-UTF-8 key) rather
  than silently changing what `key` means, or for keeping the event name under a new field (`event_name`) beside
  the bare key.
- The no-listener case is a composition rule, not a tool bug:
  `Application/Surl.Composition.UnitLibrary/SurlServerComposition.Start.cs:469-472` registers the keyspace and the
  zones as stores, and hands them to `ComposedStores`, when a `RedisServer` or a DNS server is *composed*
  (`servers.OfType<RedisServer>().Any()`, `IsDnsComposed`, lines 480-499). Every extended server is composed
  whichever listen URLs are given (the composition's `CLAUDE.md`, Azure staged-blocks bullet), so with extensions
  on the keyspace and zones are always there, and `ComposedStores.SeedKeyAsync` / `SeedZoneAsync` (lines 519-543;
  "This instance holds no Redis keyspace.") never refuse. The LDAP directory
  (`LdapDirectoryStoreRegistration.For(loaded.State.LdapServer)`) is always registered too. ADR-0158 decision 5
  says a store joins "when a redis server is composed".
- Weigh: refusing in the composition (store present only when a listen URL of `redis`/`rediss`, `dns`, or
  `ldap`/`ldaps` is served) makes `Surl.Api` (`SurlServer.Keys`, `Zones`, `Directory`, which turn the refusal into
  `SurlSeedRefusedException`), `surl-mcp` and the control endpoint (`409 conflict`) agree with no host code; it also
  changes which stores a whole-instance reset or snapshot walks, and what `read_key`/`list_keys` answer (`NotFound`,
  an empty page) on such an instance. Refusing only in `surl-mcp` leaves the other hosts permissive. Keeping today's
  behaviour (a seed of a store nobody serves is harmless) is the third option. Say which a test author is better
  served by, and why (root `CLAUDE.md`, "Surl is a stand-in": embeddable, composable, controllable).
- Stand-in: decides whether a test that seeds a store its server does not serve fails loudly at the seed, or later
  when its client finds nothing.

## Acceptance criteria

- [ ] ADR-0158 gains an amendment section marked "Decided by Claude under Stewart's delegation" that states
      `set_key`'s exact result fields (for a UTF-8 key and for a non-UTF-8 key) and why, keeping ADR-0164 decision
      9's read-tool shape in agreement.
- [ ] The amendment states, for the keyspace, the zones and the directory, whether a seed on an instance with no
      listen URL serving that store is refused, by which project, with which text in `Surl.Api`, `surl-mcp` and
      the control endpoint, and what a reset, a snapshot and the read tools do there.
- [ ] It amends decision 5's "when a redis server is composed" wording to match, and records the rejected
      alternatives.
- [ ] If the amendment keeps today's behaviour for either half, it says so plainly, and BL-1405's or BL-1410's Log
      gets a line saying what is left for it.
- [ ] No `.cs` file changes.

## Notes

- BL-1410 implements the composition half, BL-1405 the `surl-mcp` half.

## Log

- 2026-11-19: Created.
