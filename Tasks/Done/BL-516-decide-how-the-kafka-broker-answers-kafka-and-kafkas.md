---
id: BL-516
title: Decide how the Kafka broker answers kafka and kafkas
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-543, BL-401, BL-622]
touches: [Documentation/Planning/Decisions, Documentation/Conformance/Extended/Kafka.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
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
- Shared message bus (Stewart, 2026-11-04): BL-622's ADR decides one topic space shared by every messaging
  server in one `surl`, held by the horizontal library it names (planning `Surl.MessageBus`), including whether
  the per-topic log with offsets lives in the bus, and how a Kafka topic and partition map onto it. Decisions 2,
  3 and 4 below apply that (topics and the log are the bus's, not a private store; a message published over
  `mqtt` must be fetchable over `kafka`, and a produced record consumable over `mqtt`) and add only what is
  Kafka-specific, including how compressed batches reconcile with a bus that holds individual messages.
  SASL reads the one account set (ADR-0032; BL-622 decision 4).

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

- [x] An ADR with the next free number exists under `Documentation/Planning/Decisions`, marked "Decided by
      Claude under Stewart's delegation". It answers decisions 1 to 7, each citing the protocol guide section, and
      is listed in the folder's `README.md`.
- [x] `Documentation/Conformance/Extended/Kafka.md` exists in the matrix format, each "covered by" naming
      BL-517, BL-518, BL-519, BL-520 or BL-521, or "n/a: <reason>".

## Notes

- ADR-0105 written by hand in the session (docs pipeline; no `.cs` or project file changed, so the `verify` skill
  does not apply). Kafka documentation pinned: the protocol guide for Apache Kafka 4.4
  (`https://kafka.apache.org/44/design/protocol`, read 2026-11-05); each version range and error code read from it.
- Summary: one-node broker (node 0) over the shared bus; ApiVersions 0-3, Metadata 0-8, Produce 3-8, Fetch 4-11,
  ListOffsets 1-5, SaslHandshake 0-1, SaslAuthenticate 0-1; unserved API or version closes with no bytes, except the
  ApiVersions v0 `UNSUPPORTED_VERSION` fallback; one partition, auto-created topics; the bus's log; produced
  compressed batches decoded into bus messages (bounded), served batches built fresh, compressed under a new
  `--kafka-fetch-compression`; SASL PLAIN/SCRAM through the policy, required unless `--allow-anonymous`.
- Departure from the planning assumption in decision 4 and BL-521's first Goal: compressed batches are **not**
  stored as produced, because ADR-0100 decision 3.4 has the bus hold one message a record (an `mqtt` subscriber must
  see each record). BL-521 rewritten to match.
- Departure from decision 1's planning assumption: `UNSUPPORTED_VERSION` (35) only in the ApiVersions fallback; any
  other unserved version closes the connection, since its response schema does not exist.
- The new codecs library amends ADR-0002 decision 3's table (row and header line added in ADR-0002, inside this
  task's `Documentation/Planning/Decisions` touch).
- Filed (lanes may file extra tasks): BL-697 (Surl.Compression: Snappy, xerial, LZ4, XXH32), BL-698 (zstd decoder),
  BL-699 (zstd encoder), BL-700 (SCRAM-SHA-512), BL-701 (decide consumer groups, committed offsets, CreateTopics,
  InitProducerId). BL-521 now depends on BL-697 to BL-699; BL-522 gains the option.

## Log

- 2026-11-04: Created.
- 2026-11-04: Amended for Stewart's shared-stores approval of 2026-11-04: waits on BL-622; Context says the message bus is shared.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. ADR-0105 decides the Kafka broker for kafka and kafkas; Kafka.md matrix traces it to BL-517..BL-521
