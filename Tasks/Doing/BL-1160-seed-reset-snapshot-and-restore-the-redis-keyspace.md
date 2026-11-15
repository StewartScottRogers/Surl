---
id: BL-1160
title: Seed, reset, snapshot and restore the Redis keyspace
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1151, BL-1159]
touches: [Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests]
requirement: FR-061
created: 2026-11-14
completed:
---
# BL-1160 — Seed, reset, snapshot and restore the Redis keyspace

## Goal

The Redis keyspace offers seed, reset, snapshot and restore as public operations, and `RedisServer` can be built over a keyspace the composition owns, so BL-1161 can put it into `ComposedStores`.

## Context

- `Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary/RedisKeyspace.cs` is internal, has `Clear()` and a `SyncRoot` lock, and is created inside `RedisServer`'s public constructors; the constructor taking a keyspace (`RedisServer(IAuthenticationPolicy, RedisKeyspace, RedisPubSub)`) is internal.
- The operations, their input shapes, snapshot bound and events are BL-1151's ADR. Model them on `MessageBus.SeedResetSnapshot.cs` (`Protocols/Messaging/Surl.MessageBus.UnitLibrary`) and `MessageBusSnapshot` (opaque, restorable only into the store that made it): a seed writes through the same path a client's write takes and never consults the fault plan; reset and restore publish one `StoreChangedEvent` each (`Reset`, `Restored`); a seed publishes `Seeded`.
- Expiry uses the injected time (`context.TimeProvider` today); a seeded key with an expiry measures it on the `TimeProvider` the keyspace is given.

## Acceptance criteria

- [ ] A public type (name per BL-1151's ADR) exposes seed, reset, take-snapshot and restore over one keyspace, plus a public `RedisServer` constructor that takes it; existing constructors keep their behaviour.
- [ ] Named tests: a seeded key is read back by a `GET` over an in-memory connection; reset leaves every database empty; a snapshot taken, then changed, then restored gives back the snapshot's keys, values and remaining expiries; a snapshot from another keyspace is refused with `ArgumentException`; a snapshot over the bound is refused, never truncated.
- [ ] Named tests: seed, reset and restore publish `Seeded`, `Reset` and `Restored` as the ADR decides, and a seed is not stopped by an `out-of-space` rule.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the Redis library keeps 100% line and branch coverage with no method over complexity 10.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing.
