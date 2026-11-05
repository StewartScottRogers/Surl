---
id: BL-519
title: Accept Kafka Produce requests into partition logs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-518]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitTests]
requirement: none
created: 2026-11-04
completed:
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

## Acceptance criteria

- [ ] Named tests show:
      - a batch of three uncompressed records, built in the test from the Record Batch layout with its CRC-32C
        computed by hand, appended at offset 0 and the next batch at offset 3;
      - a flipped byte gets `CORRUPT_MESSAGE`;
      - magic 1 gets 43;
      - `acks` 0 gets no response;
      - `acks` 2 gets 21;
      - an oversized batch gets 10;
      - an unknown partition gets 3.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
