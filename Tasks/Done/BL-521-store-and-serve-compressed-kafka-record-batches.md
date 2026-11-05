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
completed: 2026-11-05
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

- [x] Named tests show a produced batch of each codec appended as one bus message a record with the right offsets,
      and fetched back as the same records.
- [x] Named tests refuse a codec value 5 to 7 and an undecodable payload with `CORRUPT_MESSAGE` (2), and a batch
      past the decompression bound with `MESSAGE_TOO_LARGE` (10).
- [x] Named tests show, for each of the five codec settings, a fetched batch whose attributes name the codec, whose
      CRC-32C holds, and whose records decode under that codec's specification.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- 2026-11-05, BL-516: rewritten for ADR-0105, which decodes produced batches into bus messages instead of storing
  them as produced (ADR-0100 decision 3.4: the bus holds one message a record) and puts the encoders in the new
  `Surl.Compression` library (BL-702 to BL-699), so this task depends on them.
- 2026-11-05, BL-521 delivered:
  - `KafkaCompressionCodec` (0 to 4) and `KafkaRecordsCompression` (`Decompress`, `Compress`,
    `DecompressionBound`) are new; `KafkaRecordBatchReader.Read` takes the bound (default 256 MiB),
    `KafkaProduceAnswerer` passes `DecompressionBound(--max-message)`, `KafkaFetchAnswerer` takes the
    served codec as an optional constructor parameter (default `None`, an undefined value throws), and
    `KafkaRecordBatchWriter.Write` compresses each batch with it. The library references
    `Foundation/Surl.Compression.UnitLibrary`, which ADR-0105 decision 0 allows (the reference is in
    this task's own csproj, so `touches` did not grow).
  - Choice: the Fetch byte limit counts records' uncompressed bytes as they are taken; a compressed
    batch that still passes the limit gives up its last records until it fits, and the response's
    batches end there. Why: ADR-0105 decision 4 says a batch stays within the limit, the compressed
    size is only known after compressing, and for codec none this is byte-for-byte the BL-520
    behaviour (every existing Fetch test unchanged).
  - Choice: gzip decoding reads `GZipStream` in 64 KiB pieces and stops with `MESSAGE_TOO_LARGE` (10)
    the moment the output would pass the bound; `InvalidDataException` is `CORRUPT_MESSAGE` (2).
  - Test payloads are written from each format document in `KafkaCompressedPayloads`: gzip by
    `GZipStream`, raw Snappy literals and xerial framing, an LZ4 frame of uncompressed blocks (header
    checksum 0x82 for FLG 0x60 BD 0x40), a zstd frame of one Raw_Block. Served batches are checked by
    hand CRC-32C and decoded with `GZipStream` or `Surl.Compression`'s decoders.
  - Tests: `KafkaRecordsCompressionTests` (new), `KafkaRecordBatchReaderTests.Read_ACompressedBatch*`,
    `KafkaProduceAnswererTests.ServeAsync_ACompressedBatchOfThree_*`, `_ACodecFiveToSeven_*`,
    `_AGzipBatchThatIsNotGzip_*`, `_ACompressedBatchPastFourTimesMaxMessage_*`,
    `KafkaFetchAnswererTests.ServeAsync_AProducedCompressedBatch_*`, `_EachServedCodec_*`,
    `KafkaRecordBatchWriterTests.Write_AGzipBatch*`. Kafka tests 358 green; the library is at 100%
    line and branch coverage.

## Log

- 2026-11-04: Created.
- 2026-11-05: Goal, Context and criteria rewritten for ADR-0105 by BL-516; depends on BL-702, BL-698 and BL-699.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. The Kafka broker decodes gzip, Snappy, LZ4 and zstd produced batches into one bus message a record and serves batches compressed with its constructor's codec
