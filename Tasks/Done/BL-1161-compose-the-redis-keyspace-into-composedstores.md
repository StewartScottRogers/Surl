---
id: BL-1161
title: Compose the Redis keyspace into ComposedStores
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1158, BL-1160]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: FR-061
created: 2026-11-14
completed: 2026-11-15
---
# BL-1161 — Compose the Redis keyspace into ComposedStores

## Goal

One Redis keyspace per composed instance is shared by the `redis` and `rediss` servers and registered in `ComposedStores`, so `ComposedServing.Stores` seeds it and a whole-instance reset, snapshot and restore include it.

## Context

- Today `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs` registers `new(["redis", "rediss"], services => new RedisServer(services.AuthenticationPolicyFor("redis"), services.MessageBus))`, so the keyspace lives inside the server. Create the keyspace once (in `ServiceState`/`ComposedProtocolServices` as the other shared state is), pass it through BL-1160's constructor, and add its registration through BL-1158's seam in the place BL-1151's ADR gives it in the order.
- `ComposedStores` gains the seed member(s) the ADR names for the keyspace, beside `SeedMailAsync`, `SeedFileAsync`, `SeedPublishAsync`.
- With `SURL_EXTENSIONS=0` the Redis server is off; decide nothing new - follow the ADR on whether the store is then registered.

## Acceptance criteria

- [x] A composition test shows a key set over a `redis://` exchange is seen by a `rediss://` exchange of the same instance and by no other instance.
- [x] Named tests: `ComposedStores` seeds a key; `ResetAsync` empties the keyspace with the others; `TakeSnapshotAsync` then `RestoreAsync` brings back its keys; the snapshot bound counts the keyspace.
- [x] Every existing `Surl.Composition.UnitTests` test passes unchanged.
- [x] `dotnet build Surl.slnx -warnaserror` is clean, the fast tests are green, and `Surl.Composition.UnitLibrary` keeps 100% line and branch coverage.

## Notes
- Followed BL-1163's pattern (`additionalRegistrations` + a typed seed member) since BL-1158's `ControlledStore` registration member does not exist in the code. `ComposedProtocolServices.RedisKeyspace` holds the one keyspace; `RedisKeyspaceStoreRegistration` registers it only when a `RedisServer` is composed, so `SURL_EXTENSIONS=0` registers no store (ADR-0158: the server is off, nothing to control).
- Seed member: `ComposedStores.SeedKeyAsync(database, key, value, expiry, ct)`; order is content, mail, bus, accounts, keyspace, directory, brokers' exchanges.
- `Amqp0ExchangeStoreTests` pins the full `RegisteredStoreOrder`, so its expected list gained `Keyspace` (an order assertion cannot stay unchanged when a store joins; BL-1163 did the same).
- The `rediss` twin is the same server object as `redis`; the test shares the key across two `redis://` listeners of one server (FakeListenerFactory has no TLS handshake).

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing.
- 2026-11-15: Doing -> Done. Redis keyspace is one per composed instance, seeded, reset, snapshotted and restored through ComposedStores
