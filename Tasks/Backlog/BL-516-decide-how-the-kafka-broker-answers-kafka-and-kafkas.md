---
id: BL-516
title: Decide how the Kafka broker answers kafka and kafkas
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-401]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/Kafka.md]
requirement: none
created: 2026-11-04
completed:
---
# BL-516 — Decide how the Kafka broker answers kafka and kafkas

## Goal

An ADR decides how `surl kafka://` and `kafkas://` answer as a single-node Kafka broker. It covers the APIs and
version ranges served, topics and partitions, the log and its storage, SASL, compressed record batches, error
codes, TLS and limits. A server-side requirement matrix traces each decision.

## Context

- Oracle and rules: BL-543's ADR. Specifications:
  - the Apache Kafka protocol guide (https://kafka.apache.org/protocol): framing, request and response headers,
    primitive types, each API's versioned schema, and the error-codes table;
  - the "Record Batch" and "Record" sections of https://kafka.apache.org/documentation/#recordbatch (magic 2,
    CRC-32C over attributes onward, and the codec bits);
  - RFC 4616 (SASL `PLAIN`).
  Pin the Kafka documentation version read.
- Which requests a client sends (what to accept, not a verdict): the port's `Curl.Extended.Protocol.Kafka.UnitLibrary`
  and its matrices `Kafka.md`, `Kafka-Records.md` and `Kafka-Compression.md`. Per those matrices the client sends
  request header v1, Metadata v4, Produce v3, Fetch v4, SaslHandshake v1 and SaslAuthenticate v0, produces
  uncompressed batches only, and consumes gzip, Snappy, LZ4 and zstd batches. The port's `KafkaReferenceBroker` is
  copyable code.

Decide and record:

1. The APIs and version ranges served. Planning assumption: ApiVersions, Metadata, Produce, Fetch, ListOffsets,
   SaslHandshake and SaslAuthenticate, at the versions above and the non-flexible versions around them. Also the
   answer for an API or version outside the ranges (`UNSUPPORTED_VERSION` 35, and ApiVersions v0's rule).
2. Topics: created on first produce or metadata request, or configured only; partition counts; and the
   `UNKNOWN_TOPIC_OR_PARTITION` answer.
3. The log: in memory or under the content store's `.surl/` area, retention, and offsets.
4. Record batches: the CRC-32C check (`System.Numerics.BitOperations.Crc32C`), `CORRUPT_MESSAGE`, and whether
   compressed batches from a producer are stored and served as produced (their header fields lie outside the
   compressed records, so no decompression is needed). Also whether surl can itself serve batches compressed with
   each codec, so a consumer's decompression can be exercised, and with which hand-built encoders.
5. SASL: `PLAIN` against the accounts file, required by default, with anonymous only under `--allow-anonymous`;
   whether SCRAM is offered; and what happens to a request before authentication.
6. `acks` 0, 1 and -1 on a single node, and Fetch `max_wait_ms`/`min_bytes` on an injected `TimeProvider`.
7. `kafkas` over TLS, and each ADR-0006 limit (request size, `message.max.bytes`).

## Acceptance criteria

- [ ] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 7, each citing the protocol guide section, and
      is listed in the folder's `README.md`.
- [ ] `Documentation/Conformance/Extended/Kafka.md` exists in the matrix format, each "covered by" naming
      BL-517, BL-518, BL-519, BL-520 or BL-521, or "n/a: <reason>".

## Notes

## Log

- 2026-11-04: Created.
