---
id: BL-951
title: Answer Kafka InitProducerId and check idempotent producers' sequence numbers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitTests, Documentation/Conformance/Extended/Kafka.md]
requirement: none
created: 2026-11-06
completed:
---
# BL-951 — Answer Kafka InitProducerId and check idempotent producers' sequence numbers

## Goal

The Kafka broker hands idempotent producers a producer id through InitProducerId v0-v1, refuses a transactional
id, and checks each idempotent batch's sequence so that a retried batch is answered with its first offset and never
appended twice.

## Context

- Decision: [ADR-0145](../../Documentation/Planning/Decisions/ADR-0145-how-the-kafka-broker-answers-consumer-groups-committed-offsets-createtopics-and-initproducerid.md) decisions 1, 8, 9 and 10 (amending ADR-0105 decision 4's "its sequence is not
  checked"). Specification: the Apache Kafka 4.4 protocol guide (<https://kafka.apache.org/44/design/protocol>),
  InitProducerId, Produce and the "Error Codes" table; the "Record Batch" section for `producerId`, `producerEpoch`
  and `baseSequence`.
- InitProducerId: a null `transactional_id` gets error 0, epoch 0 and the next producer id - the first drawn from
  that exchange's `ExchangeContext.Identifiers` (8 bytes big-endian masked to 47 bits), then one more each time,
  thread-safe across connections; a non-null one (empty included) is 42 with id and epoch -1.
- Produce: `KafkaRecordBatchReader` reads `producerId`, `producerEpoch`, `baseSequence` and `lastOffsetDelta` into
  its result; a new producer-state holder in the library, shared by the InitProducerId and Produce answerers, keeps
  per producer id and canonical topic the epoch and the last five batches (first and last sequence, base offset),
  at most `DefaultMaxProducerStates` 10000, least recently used dropped. Rules (decision 8): no state, any sequence
  taken; lower epoch 47; higher epoch as no state; a repeat of one of the last five 0 with its original base offset
  and nothing appended; one past the last (wrapping 2147483647 to 0) taken; otherwise 45. Checked after the record
  size check (10) and before the bus append; `producerId` -1 unchecked. A partition is a duplicate only when every
  batch is.
- `KafkaErrorCode` gains 42, 45 and 47; `KafkaServedApis` and `KafkaApiKey` gain InitProducerId (22).
- Faults: InitProducerId asks `CommandStageFaults.FindAndApplyAsync` (ADR-0141), command `InitProducerId`, no path;
  an `error` rule's code is the top-level `error_code`. Test with `ScriptedFaultPlan`.

## Acceptance criteria

- [ ] `KafkaApiVersionsAnswererTests` pins InitProducerId 0-1.
- [ ] Named `KafkaInitProducerIdAnswererTests`: two calls answered consecutive ids from a fixed-byte
      `IIdentifierRandom` test double's start (masked to 47 bits) with epoch 0; a transactional id and an empty one answered 42 with -1 and -1.
- [ ] Named `KafkaProduceAnswererTests`: a batch then its exact retry answered with the same base offset and the log
      holding it once; a gap answered 45 and nothing appended; an older epoch 47; a producer with no state accepted at
      sequence 7; sequence 2147483647 followed by 0 accepted; `producerId` -1 never checked.
- [ ] A test shows the 10001st producer state dropping the least recently used, whose next batch is then accepted
      at any sequence.
- [ ] `Kafka.md`'s InitProducerId rows name these tests in place of BL-951, and `Kafka:Batch-Producer-1` is
      removed.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-701 (ADR-0145).

## Log

- 2026-11-06: Created.
