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
completed:
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

## Acceptance criteria

- [ ] Named tests show:
      - a fetch from offset 0 returning the stored batches byte for byte;
      - a fetch from the middle of a batch returning that whole batch;
      - `partition_max_bytes` smaller than the first batch still returning it;
      - a fetch past the end with `min_bytes` 1 waiting until a produce arrives or `max_wait_ms` passes on the
        manual clock;
      - a fetch beyond the high watermark gets `OFFSET_OUT_OF_RANGE`;
      - ListOffsets earliest and latest.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
