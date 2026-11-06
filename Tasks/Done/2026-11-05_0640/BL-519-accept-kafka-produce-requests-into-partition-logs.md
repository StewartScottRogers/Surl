---
id: BL-519
title: Accept Kafka Produce requests into partition logs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-518, BL-664]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitTests, Documentation/Conformance/Extended/Kafka.md, Documentation/Planning/Decisions/ADR-0105-how-the-kafka-broker-answers-kafka-and-kafkas.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-519 — Accept Kafka Produce requests into partition logs

## Goal

The Kafka broker accepts Produce (v3 and the ADR's range). It checks each record batch's magic, length and
CRC-32C, assigns base offsets, appends to the partition log, and answers with the base offset, or with the error
code for each failure. It honours `acks` 0, 1 and -1.

## Context

- Decision: BL-516's ADR, decisions 3, 4, 6 and 7.
- Kafka protocol guide: Produce v3 (`transactional_id`, `acks`, `timeout_ms`, topic data, and the response's
  `base_offset`, `log_append_time_ms` and `throttle_time_ms`). The Record Batch section: `baseOffset`,
  `batchLength`, `partitionLeaderEpoch`, `magic` 2, `crc` (CRC-32C from `attributes` to the end), `attributes`,
  `lastOffsetDelta`, `baseTimestamp`, `maxTimestamp`, `producerId`, `producerEpoch`, `baseSequence` and the record
  count. Errors: `CORRUPT_MESSAGE` 2, `UNKNOWN_TOPIC_OR_PARTITION` 3, `MESSAGE_TOO_LARGE` 10,
  `INVALID_REQUIRED_ACKS` 21, `UNSUPPORTED_FOR_MESSAGE_FORMAT` 43.
- CRC-32C with `System.Numerics.BitOperations.Crc32C`. The log is shared by connections and owned by the server
  instance, not a static.
- **Shared message bus (Stewart, 2026-11-04; BL-622's ADR, applied by BL-516's ADR):** the partition log is the
  bus's per-topic log (BL-633) unless BL-622's ADR keeps it in the broker; topic names convert with BL-630's
  mapping; a produced record is also delivered to the bus's live subscribers (an `mqtt` subscriber sees it). The
  library references `Surl.MessageBus.UnitLibrary`. Each produce passes the connection's account and scheme to
  BL-664's access check, a refusal answered `TOPIC_AUTHORIZATION_FAILED` (29) as BL-516's ADR says.

## Acceptance criteria

- [x] Named tests show:
      - a batch of three uncompressed records, built in the test from the Record Batch layout with its CRC-32C
        computed by hand, appended at offset 0 and the next batch at offset 3;
      - a flipped byte gets `CORRUPT_MESSAGE`;
      - magic 1 gets 43;
      - `acks` 0 gets no response;
      - `acks` 2 gets 21;
      - an oversized batch gets 10;
      - an unknown partition gets 3;
      - a produced record's value delivered to a bus subscriber of the mapped topic;
      - a produce refused by topic access gets the ADR's error code.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Built: `KafkaCrc32C`, `KafkaRecordBatchReader` (+ `KafkaProducedRecord`, `KafkaRecordBatchReadResult`),
  `KafkaProduceAnswerer`, and `KafkaPrimitiveReader.ReadRawBytes` for record fields. Tests:
  `KafkaProduceAnswererTests`, `KafkaRecordBatchReaderTests`, `KafkaCrc32CTests`; batches are built by
  `KafkaRecordBatches` with a bit-by-bit CRC-32C independent of the library's.
- Acceptance tests: three records at offset 0 then the next at 3
  (`ServeAsync_ABatchOfThreeThenABatchOfOne_AreAppendedAtOffsetZeroThenThree`); flipped byte
  (`ServeAsync_AFlippedByte_IsCorruptMessageAndNothingIsAppended`); magic 1 (`ServeAsync_MagicOne_IsUnsupportedForMessageFormat`);
  acks 0 (`ServeAsync_AcksZero_WritesNoResponseAndAppends`); acks 2 (`ServeAsync_AcksOtherThanZeroOneOrMinusOne_...`);
  oversized (`ServeAsync_ARecordPastTheBusMessageLimit_IsMessageTooLarge` - "oversized" read as ADR-0105
  decision 7's per-record `--max-filesize` bound, the bus's `MaxMessageBytes`, since an uncompressed batch has
  no other size limit beyond `--max-message` on the frame); unknown partition
  (`ServeAsync_AnUnknownPartition_IsUnknownTopicOrPartition`); bus subscriber
  (`ServeAsync_AProducedRecord_IsDeliveredToABusSubscriberOfTheMappedTopic`); topic access, 29
  (`ServeAsync_ATopicTheCallerMayNotPublishTo_IsTopicAuthorizationFailed`).
- Decisions recorded in ADR-0105's new "Amendment, 2026-11-05 (BL-519)": codecs 1-4 are 43 until BL-521;
  null/empty records, count < 1, negative header count and null header key are 2; a failed partition answers
  -1 offsets and time; acks checked before transactional_id; topic checks before batch checks.
- Touches widened (no task in Doing names them): `Documentation/Conformance/Extended/Kafka.md` (its BL-519
  rows now name the tests, as ADR-0105 asks) and ADR-0105 (the amendment).
- Known gap, filed as BL-754: the bus has no all-or-nothing multi-message append, so a `StoreFull` met part
  of the way through a partition keeps the earlier records (answered 44). Every check the broker can make
  is made before the first publish.
- Coverage: `Measure-CodeQuality.ps1 -Library Surl.Extended.Protocol.Kafka.UnitLibrary` reports 100% line and
  100% branch. It still lists `KafkaBrokerConnection.ServeAsync` and `AnswerReadableAsync` at complexity 12
  (async state-machine count); both predate this task, are unchanged here, and pass the build's CA1502.

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-664; partition logs are Surl.MessageBus's.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Kafka Produce v3-v8 checks record batches (length, magic, CRC-32C, records), appends to the bus's topic logs, answers base offsets or per-partition errors, honours acks 0/1/-1
