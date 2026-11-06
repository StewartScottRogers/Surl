---
id: BL-866
title: Hand the Redis server the shared MessageBus in composition and map its channels through MessageBus.Names
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-492, BL-678]
touches: [Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests, Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitLibrary, Protocols/DataStore/Surl.Extended.Protocol.Redis.UnitTests]
requirement: FR-058
created: 2026-11-05
completed:
---
# BL-866 — Hand the Redis server the shared MessageBus in composition and map its channels through MessageBus.Names

## Goal

A `surl` process serving `redis` and `mqtt` at once delivers a Redis `PUBLISH a/b x` to an MQTT
subscriber of `a/b` and back, and a `--topic-map` alias applies to Redis channels as it does to
every other messaging scheme.

## Context

- BL-678 (ADR-0126) moved Redis pub/sub onto `Surl.MessageBus` through
  `RedisServer(IAuthenticationPolicy, MessageBus)`; the one-argument constructor makes a bus of its own.
- BL-492 serves Redis from the executable. Whichever registration it adds in
  `Surl.Composition`'s `ProtocolServerRegistrations` must use `services.MessageBus`, as the MQTT row does.
- `MessageBus.Names` (`TopicNameLookup`) has no `redis`/`rediss` default mapping
  (`Surl.MessageBus.UnitLibrary/CLAUDE.md`: "Redis waits for BL-678"). Add an identity mapping
  per ADR-0126 decision 1 and have `RedisChannelNames` map through it.

## Acceptance criteria

- [ ] A `Surl.Composition.UnitTests` test composes `redis` and `mqtt` and shows the Redis server and
      the MQTT server hold the same `MessageBus` instance.
- [ ] `TopicNameLookup` maps the `redis` and `rediss` schemes, with a unit test of a `--topic-map`
      alias applied both ways to a Redis channel.
- [ ] The `Surl.MessageBus.UnitLibrary/CLAUDE.md` line "Redis waits for BL-678" is replaced by what is true.
- [ ] `dotnet build` is clean and the fast tests are green; the touched libraries keep 100% line and
      branch coverage.

## Notes

- Filed by BL-678.

## Log

- 2026-11-05: Created.
