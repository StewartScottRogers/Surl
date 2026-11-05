---
id: BL-521
title: Store and serve compressed Kafka record batches
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-520, BL-702, BL-698, BL-699]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-521 — Store and serve compressed Kafka record batches

## Goal

The Kafka broker accepts record batches compressed with gzip, Snappy (raw or xerial), LZ4 or zstd (attribute codec
bits 1 to 4), decodes them into one bus message a record, and serves the batches it builds compressed with the codec
the broker is constructed with (`--kafka-fetch-compression`, ADR-0105 decision 4).

## Context

- Decision: ADR-0105 decision 4 (decode on produce, the decompression bound and `MESSAGE_TOO_LARGE` (10), codec 5 to
  7 and undecodable payloads `CORRUPT_MESSAGE` (2), the served batch's attributes, the encoders). Matrix rows
  `Kafka:Batch-Compression-*`, `Kafka:Surl-Compression-1` and `-2`, `Kafka:RFC1952-2.3-1`, `Kafka:RFC8878-3.1.1-1`
  in `Documentation/Conformance/Extended/Kafka.md`.
- Codecs: gzip through the BCL's `GZipStream`; Snappy, xerial, LZ4 and zstd through `Surl.Compression`
  (BL-702 to BL-699). The library references `Foundation/Surl.Compression.UnitLibrary` (ADR-0002 as ADR-0105
  amends it).
- Test batches: built in the tests from the format documents (gzip with `GZipStream`; zstd, LZ4 and Snappy frames
  written by hand, as BL-702 to BL-699's tests do), never from the Curl port's output.
- The codec of served batches is a constructor parameter here; BL-522 adds the option that sets it.

## Acceptance criteria

- [ ] Named tests show a produced batch of each codec appended as one bus message a record with the right offsets,
      and fetched back as the same records.
- [ ] Named tests refuse a codec value 5 to 7 and an undecodable payload with `CORRUPT_MESSAGE` (2), and a batch
      past the decompression bound with `MESSAGE_TOO_LARGE` (10).
- [ ] Named tests show, for each of the five codec settings, a fetched batch whose attributes name the codec, whose
      CRC-32C holds, and whose records decode under that codec's specification.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- 2026-11-05, BL-516: rewritten for ADR-0105, which decodes produced batches into bus messages instead of storing
  them as produced (ADR-0100 decision 3.4: the bus holds one message a record) and puts the encoders in the new
  `Surl.Compression` library (BL-702 to BL-699), so this task depends on them.

## Log

- 2026-11-04: Created.
- 2026-11-05: Goal, Context and criteria rewritten for ADR-0105 by BL-516; depends on BL-702, BL-698 and BL-699.
- 2026-11-05: Backlog -> Doing.
