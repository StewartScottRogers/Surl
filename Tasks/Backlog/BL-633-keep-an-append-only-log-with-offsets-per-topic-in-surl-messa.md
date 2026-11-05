---
id: BL-633
title: Keep an append-only log with offsets per topic in Surl.MessageBus
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-629]
touches: [Protocols/Messaging/Surl.MessageBus.UnitLibrary, Protocols/Messaging/Surl.MessageBus.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-633 — Keep an append-only log with offsets per topic in Surl.MessageBus

## Goal

`Surl.MessageBus` keeps, for each topic the ADR names, an append-only log of the messages published to it, each
with an offset, so a Kafka consumer can fetch from an offset what any protocol published, and a Kafka producer's
records reach every other protocol's subscribers.

## Context

- Decision: BL-622's ADR, decision 3 ("an append-only log per topic with offsets for Kafka, or say which stay
  inside one broker"). If the ADR keeps the log inside the Kafka broker instead, this task has no work: record
  that in Notes, remove BL-633 from BL-634's `depends-on`, and move this task to `Deferred` with the ADR's
  decision as the reason; BL-519 and BL-520 then keep their own log.
- What a log holds and serves, from the Apache Kafka protocol guide (https://kafka.apache.org/protocol) as the
  ADR cites it: per partition, a log start offset, a high watermark, records with offset, timestamp, key, value
  and headers; Fetch from an offset; ListOffsets earliest and latest. Partitions per topic as the ADR decides.
- Retention and bounds as the ADR says (bytes per topic, oldest dropped first, the log start offset moving up).
  Time from the injected `TimeProvider`.
- Library: BL-629's `Surl.MessageBus`. Persistence is BL-634's.

## Acceptance criteria

- [ ] Named tests show: a message published on the bus appears in its topic's log at the next offset; reading
      from an offset returns the records from there in order; reading past the high watermark returns none;
      retention past the bound drops the oldest and raises the log start offset; reading below the log start
      returns the ADR's typed outcome (Kafka's `OFFSET_OUT_OF_RANGE` is the broker's word for it); a record
      appended through the log's producer side is delivered to live subscribers too.
- [ ] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
