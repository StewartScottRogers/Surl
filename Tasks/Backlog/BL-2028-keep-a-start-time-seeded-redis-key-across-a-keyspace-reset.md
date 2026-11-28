---
id: BL-2028
title: Keep a start-time seeded Redis key across a keyspace reset
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests]
requirement: FR-061
created: 2026-11-27
completed:
---
# BL-2028 — Keep a start-time seeded Redis key across a keyspace reset

## Goal

`RedisKeyspace.SeedKey` can mark a seed as part of the configured state, and `RedisKeyspace.Reset` returns to exactly those keys.

## Context

- ADR-0158 decision 4: a keyspace reset leaves all 16 databases empty, then the host's start-time keys, with their expiry deadlines measured again from the reset on the instance's clock. Decision 3: start-time seeds go through the same seed operation and publish `Seeded` as any seed does.
- Today `RedisKeyspace.Reset` (`Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary/RedisKeyspace.cs`) only empties the databases (`ReplaceAll` of empty snapshots), and `SeedKey(database, key, value, expiry)` records nothing to return to.
- Give `SeedKey` an optional last parameter `bool keepOnReset = false`, the name `DnsZoneSet.SeedZone` already uses. Keep a private copy of each kept key's database, name, value bytes and lifetime (not its deadline), so a later command that changes the live value does not change what reset returns to. A later kept seed of the same database and key replaces the earlier one. A seed without the flag, and every client command, leaves the kept set unchanged. A refused seed keeps nothing.
- Reset publishes one `StoreChange.Reset` (empty name, 0 bytes) and no `Seeded` or `Written` event (ADR-0158 decision 2); drain the `Journal` of anything writing the kept keys back records. Snapshot and restore do not change the kept set.
- Composition calls the flag in BL-2030; nothing outside this library and its tests changes here. The library's `CLAUDE.md` and the XML remarks on `RedisKeyspace` must say what reset returns to.
- Stand-in: this is the Redis keyspace store. It publishes the events above, a test seeds and resets it, and a seed never consults the fault plan.

## Acceptance criteria

- [ ] Named tests in `Surl.Extended.Protocol.Redis.UnitTests`: after a kept seed, a client-side change or `DEL` of that key and an unkept seed of another key, `Reset` leaves exactly the kept key with its seeded value in its database, readable with `GET` on an open connection.
- [ ] Named test over a fake `TimeProvider`: a kept key seeded with an expiry lives that long again measured from the reset, including after it had already expired before the reset.
- [ ] Named tests: a reset with kept keys publishes exactly one `StoreChangedEvent`, `Reset`; a second reset gives the same contents; a later kept seed of the same key wins; a refused kept seed adds nothing to what reset returns to.
- [ ] Named test: after `Restore` of a snapshot, `Reset` still returns to the kept keys.
- [ ] `SeedKey` without the flag behaves as before; the XML comments and the library `CLAUDE.md` state what `Reset` returns to.
- [ ] `dotnet build Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests -warnaserror` is clean, the fast tests pass without `TestCategory=Integration`, and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-27: Created.
