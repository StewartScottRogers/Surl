---
id: BL-850
title: Draw the Kafka cluster ID and the NATS server_id from the instance's seedable identifier source
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-822, BL-851, BL-997]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitTests, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitTests, Application/Surl.Composition.UnitLibrary, Application/Surl.Composition.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-850 — Draw the Kafka cluster ID and the NATS server_id from the instance's seedable identifier source

## Goal

The Kafka `cluster_id` and the NATS `server_id`/`server_name`, both drawn once when the server is created, come
from the instance's server-life seedable identifier source, so a seeded instance announces the same IDs every run.

## Context

- Decision: ADR-0124 decision 6 names NATS `server_id` as seedable; a Kafka cluster ID likewise grants nothing.
  Found by BL-786's audit.
- `Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary/KafkaMetadataAnswerer.cs:59` -
  `ClusterId = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16))` in the constructor.
- `Protocols/Messaging/Surl.Extended.Protocol.Nats.UnitLibrary/NatsServer.cs:86` -
  `ServerId = RandomNumberGenerator.GetString(ServerIdAlphabet, ServerIdLength)` in the constructor.
- These are drawn per server, not per exchange, so they cannot use `ExchangeContext.Identifiers`: each constructor
  takes an `IIdentifierRandom` (defaulting to `CryptographicIdentifierRandom`), and `Surl.Composition` passes the
  instance's server-life source that BL-852 exposes (derived from the seed and a name per server, so adding a
  server never shifts another's ID).
- Keep each ID's shape: 16 bytes base64url for Kafka, 22 characters of the NATS alphabet (draw with rejection
  sampling or a modulo-free mapping so the alphabet stays uniform).

## Acceptance criteria

- [ ] `KafkaMetadataAnswererTests.ClusterId_SameSeededSource_IsTheSameEveryRun` and
      `NatsServerTests.ServerId_SameSeededSource_IsTheSameEveryRun` pass; different seeds give different IDs.
- [ ] A composition test shows two compositions with seed 42 announce the same NATS `server_id`, and two unseeded
      ones announce different ones.
- [ ] The touched libraries stay at 100% line and branch coverage; `dotnet build` clean, fast tests green.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-06: depends-on BL-852 replaced by BL-997 (BL-852 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
- 2026-11-06: Backlog -> Deferred. Split into BL-999, BL-1000 and BL-1001 (over the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06)
