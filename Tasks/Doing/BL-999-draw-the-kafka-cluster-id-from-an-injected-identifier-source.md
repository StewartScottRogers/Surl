---
id: BL-999
title: Draw the Kafka cluster ID from an injected identifier source
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-822, BL-851, BL-1035]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitTests]
requirement: none
created: 2026-11-06
completed:
---
# BL-999 — Draw the Kafka cluster ID from an injected identifier source

## Goal

The Kafka `cluster_id`, drawn once when the broker is created, comes from an `IIdentifierRandom` the broker is
given (defaulting to `CryptographicIdentifierRandom`), so a seeded source announces the same ID every run.

## Context

- First of the three tasks BL-850 ("Draw the Kafka cluster ID and the NATS server_id from the instance's seedable
  identifier source") was split into on 2026-11-06 for the 60-minute size limit: BL-999 Kafka, BL-1000 NATS, then
  BL-1001 (the composition passes the instance's server-life source).
- Decision: ADR-0124 decision 6; a Kafka cluster ID grants nothing. Found by BL-786's audit.
- `Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary/KafkaMetadataAnswerer.cs:59` -
  `ClusterId = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16))` in the constructor. It is drawn per
  server, not per exchange, so it cannot use `ExchangeContext.Identifiers`: the constructor takes the source.
- Keep the shape: 16 bytes, base64url.
- Stand-in: a seeded instance announces a repeatable cluster ID.

## Acceptance criteria

- [ ] `KafkaMetadataAnswererTests.ClusterId_SameSeededSource_IsTheSameEveryRun` passes; different seeds give
      different IDs; the ID is 16 bytes base64url.
- [ ] No `RandomNumberGenerator` call is left for the cluster ID.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- 2026-11-11 (lane 5): Parked behind BL-1035. `dotnet` cannot resolve the SDK `global.json` pins (10.0.401;
  only 10.0.100-rc.1, 11.0.100-rc.2 and 11.0.100 are installed), so no build or test can run to pass the
  gates. The code was written and left uncommitted for the shift to stash:
  - `KafkaMetadataAnswerer(MessageBus bus, IIdentifierRandom identifiers)`: the source is a required
    parameter, drawn once for 16 bytes, base64url; `System.Security.Cryptography` is gone from the file.
  - Choice: required, not defaulted to `CryptographicIdentifierRandom`. That type lives in `Surl.Core`,
    which the Kafka library does not and may not reference (ADR-0105 decision 0 names only
    `Surl.Protocol.Abstractions`, `Surl.MessageBus`, `Surl.Compression`), and no production code constructs
    the answerer yet; the composition passes the unseeded or seeded source (BL-1001).
  - Tests: `SeededRandomIdentifierRandom` (test fake over `System.Random(seed)`), new
    `Constructor_NullIdentifiers_Throws`, `ClusterId_Is16BytesOfTheSourceInBase64Url`,
    `ClusterId_SameSeededSource_IsTheSameEveryRun`, `ClusterId_DifferentSeeds_Differ`; every existing
    `new KafkaMetadataAnswerer(bus)` and `EmptyBusMetadata()` passes a seeded source. CLAUDE.md updated.
  - If the stash is lost, redo it from these notes: about 15 minutes.

## Log

- 2026-11-06: Created.
- 2026-11-11: Backlog -> Doing.
- 2026-11-11: Doing -> Backlog. Waits on BL-1035: pinned .NET SDK 10.0.401 is not installed, so nothing builds or tests
- 2026-11-11: Backlog -> Doing.
