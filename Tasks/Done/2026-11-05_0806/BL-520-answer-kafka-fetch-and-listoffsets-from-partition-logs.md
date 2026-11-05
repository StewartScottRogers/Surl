---
id: BL-520
title: Answer Kafka Fetch and ListOffsets from partition logs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-519]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-520 — Answer Kafka Fetch and ListOffsets from partition logs

## Goal

The Kafka broker answers Fetch (v4 and the ADR's range) from a partition offset with whole batches up to the byte
limits, the high watermark and last stable offset, and `OFFSET_OUT_OF_RANGE` past the log. It waits for
`min_bytes` up to `max_wait_ms`, and answers ListOffsets for the earliest and latest offsets.

## Context

- Decision: BL-516's ADR, decisions 1, 3 and 6.
- Kafka protocol guide: Fetch v4 (`max_wait_ms`, `min_bytes`, `max_bytes`, `isolation_level`, per-partition
  `fetch_offset` and `partition_max_bytes`; the response's `high_watermark`, `last_stable_offset`,
  `aborted_transactions` and `records`, and the rule that the first batch is returned whole even when it exceeds
  the limit), ListOffsets v1 (timestamp -2 earliest, -1 latest), and `OFFSET_OUT_OF_RANGE` 1.
- Time from `ExchangeContext.TimeProvider`.
- **Shared message bus (Stewart, 2026-11-04; BL-622's ADR):** fetches read the bus's per-topic log (BL-633), so a
  message published over `mqtt` is fetchable here as a record built as BL-622's ADR says (its offset, timestamp,
  key and headers). Each fetch passes the connection's account and scheme to BL-664's access check.

## Acceptance criteria

- [x] Named tests show:
      - a fetch from offset 0 returning the stored batches byte for byte;
      - a fetch from the middle of a batch returning that whole batch;
      - `partition_max_bytes` smaller than the first batch still returning it;
      - a fetch past the end with `min_bytes` 1 waiting until a produce arrives or `max_wait_ms` passes on the
        manual clock;
      - a fetch beyond the high watermark gets `OFFSET_OUT_OF_RANGE`;
      - ListOffsets earliest and latest;
      - a message published on the bus under an MQTT name fetched as a record of the mapped topic;
      - a fetch refused by topic access gets the ADR's error code.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

Plan: `KafkaFetchAnswerer` (v4 to v11) and `KafkaListOffsetsAnswerer` (v1 to v5) read
`MessageBus.ReadLog` / `GetLogOffsets`; `KafkaRecordBatchWriter` builds served batches;
`KafkaFetchableTopic` holds the topic checks both share. Choices made where ADR-0105 and the guide
leave room (decided by Claude under Stewart's delegation; BL-763 copies them into ADR-0105, since
BL-452 held `Documentation/Planning/Decisions` in Doing):

- **"Fetch from the middle of a batch returns that whole batch"** is read through ADR-0105
  decision 4: producer batch boundaries are not kept, and a served batch starts exactly at
  `fetch_offset`, so a fetch from the middle of a produced batch gets one batch holding the rest of
  it (test `ServeAsync_FromTheMiddleOfAProducedBatch_ReturnsTheRestOfThatBatchWholeFromTheOffsetAsked`).
- **The first batch whole when over the limit** means the response's first record is always
  written: the smallest batch that makes progress. Later partitions get records only within
  what is left of `max_bytes`; with nothing left they answer zero-length `records`.
- **Limits count encoded bytes** (61-byte batch header plus each encoded record), not the bus's
  `LogRecordBytes`; the bus read is bounded by the same number.
- **Topic check order** for Fetch and ListOffsets: 17, partition 3, access 29, then no log 3 -
  access before existence, so a refused caller cannot probe which topics exist (as Metadata).
  Fetch and ListOffsets need Subscribe rights (ADR-0102's Kafka row).
- **A failed partition** answers -1 for `high_watermark`, `last_stable_offset`,
  `log_start_offset` (Fetch) and `timestamp`, `offset`, `leader_epoch` (ListOffsets), and
  zero-length `records`, as BL-519 did for Produce.
- **`aborted_transactions`** is always an empty array (count 0), never null; empty `records` is
  zero length, never null.
- **The wait** subscribes a bus connection under the caller's account to each readable topic,
  then re-reads on each delivery; once the deadline passes it reads once more, so a message
  delivered as the deadline fires is not lost. Negative `max_wait_ms` or `min_bytes` is no wait.
- **Fetch sessions**: only `session_id` above 0 is 70; `session_epoch` is not checked.
- **ListOffsets** answers `timestamp` -1 for -2 and -1 (ADR), and `leader_epoch` 0 on success.
- Test helpers: `KafkaTestExchanges.Context` gained an optional `timeProvider`;
  `KafkaRecordBatches` gained `TimedRecord` and `ServedBatch`.

Verified: library 100% line and branch coverage (cobertura from `dotnet test --collect "Code
Coverage"`); 293 tests in `Surl.Extended.Protocol.Kafka.UnitTests` pass.

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: fetches read Surl.MessageBus's topic logs.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Kafka Fetch v4-v11 and ListOffsets v1-v5 answer from the bus's topic logs, with min_bytes waits on the injected clock
