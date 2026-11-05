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
completed: 2026-11-05
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

- [x] Named tests show: a message published on the bus appears in its topic's log at the next offset; reading
      from an offset returns the records from there in order; reading past the high watermark returns none;
      retention past the bound drops the oldest and raises the log start offset; reading below the log start
      returns the ADR's typed outcome (Kafka's `OFFSET_OUT_OF_RANGE` is the broker's word for it); a record
      appended through the log's producer side is delivered to live subscribers too.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the
      library keeps 100% line and branch coverage.

## Notes

- ADR-0100 decision 3.3 keeps a log for every topic on the bus (not inside the Kafka broker), so this task has
  work. One partition per topic (decision 3.2).
- Built: `TopicLog` (internal), `MessageBus.Logs.cs` (`ReadLog`, `GetLogOffsets`, `GetLogTopics`,
  `LogRecordBytes`, `MaxLogBytesPerTopic`, `MaxLogBytes` with the ADR's defaults), `BusLogRecord`, `BusLogOffsets`,
  `BusLogReadResult`, `BusPublishResult.LogOffset`, `MessageBusOutcome.OffsetOutOfRange`. The "producer side" is
  `Publish` itself: a Kafka producer's record goes through it, so it reaches fan-out, bound queues and the log in
  one step, and `LogOffset` is the base offset its Produce answers.
- Tests: `MessageBusLogTests` (19). The acceptance names map to `Publish_OnATopic_AppendsToItsLogAtTheNextOffset`,
  `ReadLog_FromAnOffset_ReturnsTheRecordsFromThereInOrderWithTimestampKeyAndHeaders`,
  `ReadLog_AtTheHighWatermark_ReturnsNoRecords`, `Publish_PastMaxLogBytesPerTopic_DropsTheOldestAndRaisesTheLogStartOffset`,
  `ReadLog_BelowTheLogStartOffset_ReturnsOffsetOutOfRangeAndNoRecords` and
  `Publish_AKafkaStyleRecord_IsDeliveredToLiveSubscribersAndReadableFromTheLog`. Library coverage 100% line, 100% branch.
- Decided by Claude under Stewart's delegation (recorded in the library's CLAUDE.md; BL-697 adds them to ADR-0100,
  since `Documentation/Planning/Decisions` is outside this task's `touches`):
  - "Reading past the high watermark returns none" is read as: at the high watermark, `Succeeded` and no records;
    above it, `OffsetOutOfRange`, as a Kafka broker answers a fetch beyond its log end.
  - A record counts payload + key + header UTF-8 + 16 bytes (offset and timestamp), so empty-payload records
    still count and no log holds unboundedly many.
  - Retention keeps the newest record even when it alone passes `MaxLogBytesPerTopic`, so a Kafka consumer can
    still read the latest message; `MaxMessageBytes` already bounds one payload.
  - `ReadLog` returns at least one record whatever `maxBytes` says, as Kafka's Fetch does, so a large record
    never stalls a consumer.
  - `MaxTopics` now counts topics with a log, as ADR-0100 decision 3.6's table says. So the BL-629 test
    `Publish_NotRetainedAtMaxTopics_IsDelivered` became `..._ReturnsTooManyTopicsBecauseEveryTopicHasALog`,
    and two retained tests that used `maxTopics: 1` to prove a freed slot now bound retained bytes instead.
    `Retain`'s own topic check was removed: a retained topic always has a log, so it could never fire.
  - Tests that compared a whole `BusPublishResult` now include its `LogOffset`.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.MessageBus keeps an append-only log per topic with offsets, retention and OffsetOutOfRange; every publish is logged and Kafka-style records reach live subscribers
