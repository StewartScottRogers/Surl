---
id: BL-1151
title: Decide how the Redis keyspace, the LDAP directory and the DNS zones are seeded, reset, snapshotted, restored and observed
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: FR-061
created: 2026-11-14
completed: 2026-11-14
---
# BL-1151 — Decide how the Redis keyspace, the LDAP directory and the DNS zones are seeded, reset, snapshotted, restored and observed

## Goal

A new ADR, marked "Decided by Claude under Stewart's delegation", settles how the three protocol-owned stores outside ADR-0100's four - the Redis keyspace, the LDAP directory and the DNS zones - are seeded, reset, snapshotted, restored and observed, so the tasks that build it (BL-1157 to BL-1169) need ask nothing.

## Context

- Stand-in audit 2026-11-14 (planning session, `/task-plan` "Plan the stand-in gaps"). ADR-0124 decisions 1 to 3 give the four shared stores (`ContentStore`, `MailboxStore`, `MessageBus`, `AccountBook`) seed, reset, snapshot, restore and `StoreChangedEvent`; ADR-0124 decision 2 says stores are not replaceable and "a later ADR may open this if a real use appears". Three stores were left out:
  - **Redis keyspace** - `Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary/RedisKeyspace.cs` (internal, built inside `RedisServer`'s constructor, memory only, ADR-0106). It publishes no `StoreChangedEvent`, never consults the fault plan at `FaultStage.StoreWrite` (only its own `maxmemory`-style bound), and no host can seed, reset or snapshot it. A test that uses Surl as a Redis stand-in leaks keys from one test into the next.
  - **LDAP directory** - `Protocols/Messaging/Surl.Protocol.Ldap.UnitLibrary/LdapDirectory.cs`, read once at start from `<path>/.surl/ldap/directory.ldif` (ADR-0072 decision 1) and empty without `--directory`. In memory - the way `Surl.Api` embeds - an LDAP stand-in therefore serves nothing, and no host can add entries.
  - **DNS zones** - `Protocols/Lookup/Surl.Extended.Protocol.DnsQuery.UnitLibrary/DnsZoneSet.cs` (`Add(fileName, content)` exists), loaded by `SurlServerComposition.LoadDnsZonesAsync` from the data directory only, so in memory the DNS server answers no zone.
- `ComposedStores` (`Application/Surl.Composition.UnitLibrary/ComposedStores.cs`) hard-codes the four stores in `StoreOrder` and a private `StoreControl` record; adding a store means editing it (the "open for extension" principle wants a registration).
- Hosts: `Surl.Api` (`SurlServerFiles`, `SurlServerMail`, `SurlServerMessages`, `SurlServerAccounts`), `Surl.Mcp` (`seed_mail`, `put_file`, `publish_message`, `add_account`), the control endpoint (`IControlTarget`, routes in `ControlEndpoint.Routes`).
- Standing rules: base class library only; secure by default; configuration only grows; a seed goes through the path a client writes by and never consults the fault plan (ADR-0124 decision 3); stores on disk refuse reset, snapshot and restore.

## Acceptance criteria

- [x] A new ADR under `Documentation/Planning/Decisions/` (next free number) decides, for each of the three stores: the `StoreKind` member it publishes as (enum only grows), which changes publish `StoreChangedEvent` and with which `StoreChange` and `Name`, the seed operation and its input shape (for example Redis string keys with optional expiry, LDIF content records, zone-file text), what reset returns to, what a snapshot holds and its bound, and its place in ADR-0124 decision 3's reset and restore order.
- [x] It decides whether a directory or zone loaded from `--directory` is "a store on disk" (refusing reset, snapshot and restore) or an in-memory copy of a file read once, and says why.
- [x] It decides how the Redis keyspace applies `out-of-space` at `FaultStage.StoreWrite`, naming the RESP error it answers (Redis's own `-OOM ...` reply as Redis documents it).
- [x] It decides the registration seam in `Surl.Composition` by which a store joins `ComposedStores`' reset, snapshot and restore (the shape BL-1158 builds), and amends ADR-0124 decision 2 accordingly.
- [x] It names the host spellings: the `Surl.Api` members, the `Surl.Mcp` tool names and properties (amending ADR-0124 decision 8), and the control endpoint routes and bodies (amending ADR-0124 decision 7), each with its bounds and refusals.
- [x] ADR-0124 gains an "Amended by" line pointing at the new ADR, and the new ADR lists BL-1157 to BL-1169 under "Acted on by".

## Notes

- Wrote ADR-0158 (Decided by Claude under Stewart's delegation) covering every acceptance criterion; amended ADR-0124 (Amended by line). Choices taken: start-time seeds re-applied on reset; strings only for Redis seeds; no DELETE routes; `--directory` LDAP and DNS are in-memory copies, so controllable.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing.
- 2026-11-14: Doing -> Done. ADR-0158 decides seed, reset, snapshot, restore and events for the Redis keyspace, LDAP directory and DNS zones
