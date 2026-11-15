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
completed:
---
# BL-1161 — Compose the Redis keyspace into ComposedStores

## Goal

One Redis keyspace per composed instance is shared by the `redis` and `rediss` servers and registered in `ComposedStores`, so `ComposedServing.Stores` seeds it and a whole-instance reset, snapshot and restore include it.

## Context

- Today `Application/Surl.Composition.UnitLibrary/ExtendedProtocolServers.cs` registers `new(["redis", "rediss"], services => new RedisServer(services.AuthenticationPolicyFor("redis"), services.MessageBus))`, so the keyspace lives inside the server. Create the keyspace once (in `ServiceState`/`ComposedProtocolServices` as the other shared state is), pass it through BL-1160's constructor, and add its registration through BL-1158's seam in the place BL-1151's ADR gives it in the order.
- `ComposedStores` gains the seed member(s) the ADR names for the keyspace, beside `SeedMailAsync`, `SeedFileAsync`, `SeedPublishAsync`.
- With `SURL_EXTENSIONS=0` the Redis server is off; decide nothing new - follow the ADR on whether the store is then registered.

## Acceptance criteria

- [ ] A composition test shows a key set over a `redis://` exchange is seen by a `rediss://` exchange of the same instance and by no other instance.
- [ ] Named tests: `ComposedStores` seeds a key; `ResetAsync` empties the keyspace with the others; `TakeSnapshotAsync` then `RestoreAsync` brings back its keys; the snapshot bound counts the keyspace.
- [ ] Every existing `Surl.Composition.UnitTests` test passes unchanged.
- [ ] `dotnet build Surl.slnx -warnaserror` is clean, the fast tests are green, and `Surl.Composition.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-14: Created.
