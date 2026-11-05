---
id: BL-521
title: Store and serve compressed Kafka record batches
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-520]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-521 — Store and serve compressed Kafka record batches

## Goal

The Kafka broker accepts record batches compressed with gzip, Snappy, LZ4 or zstd (attribute codec bits 1 to 4),
stores them as produced, and serves them unchanged on Fetch. It also serves compressed batches of its own, if
BL-516's ADR decides surl can.

## Context

- Decision: BL-516's ADR, decision 4.
- The Record Batch section: the codec bits of `attributes`, and the record count and `lastOffsetDelta`, which sit
  outside the compressed records, so offsets are assigned without decompressing.
- Test batches:
  - gzip, made with the BCL's `GZipStream`;
  - zstd, a frame with one raw block built by hand from RFC 8878 section 3.1.1;
  - LZ4, a frame with one uncompressed block built from lz4's `doc/lz4_Frame_format.md`;
  - Snappy, a literal-only stream from google/snappy's `format_description.txt`, in the xerial framing if the
    ADR says Kafka's Java producer's framing is served.
- If the ADR has surl compress batches itself, the encoders it chose are written here, each in its own type with
  its own tests. If the ADR puts them in a separate library, stop and file that library as its own task through
  `task-planner`.

## Acceptance criteria

- [ ] Named tests show a batch of each codec appended with the right offsets and fetched back byte for byte, an
      unknown codec value (5 to 7) refused as the ADR says, and each self-compressed batch decoding under its
      specification if the ADR adds them.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
