---
id: BL-523
title: Run the Curl port's kafka client against a live surl
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-973, BL-406, BL-974]
touches: [Conformance/Surl.Conformance.UnitTests, Documentation/Conformance/Extended/Kafka.md]
requirement: none
created: 2026-11-04
completed: 2026-11-12
---
# BL-523 — Run the Curl port's kafka client against a live surl

## Goal

`Integration` tests run the pinned Curl port client against a live `surl kafka://` and `kafkas://`: produce a
record with a `PLAIN` login, consume it from offset 0, consume a compressed batch, and a refused bad password.
Each assertion comes from the Kafka protocol guide, and the matrix is fully covered.

## Context

- Rules: BL-543's ADR, decisions 1 to 3 (the port is only a live client; report a port defect in Notes).
- The locator and runner: BL-406. A live `surl`: `SurlOnLoopback`. Command lines: the port's
  `curl --extended-help kafka` and its cases in `Curl.Conformance.Extended.UnitTests/Kafka/`.
- `[TestCategory("Integration")]`, and inconclusive without the pinned client.

## Acceptance criteria

- [x] The test class exists with the four exchanges, each passing on Windows with the pinned client present.
- [x] `Documentation/Conformance/Extended/Kafka.md`: every row is covered by a named test or marked `n/a` with a reason.
- [x] The fast tests are green, and `dotnet build -warnaserror` is clean.

## Notes

- `CurlPortClientExchangesRecordsWithSurlOverKafkaTests` (9 cases, all pass on Windows with the pinned client): produce then
  consume from offset 0 over `kafka` and `kafkas`; two records in order and from `?offset=1`; a gzip, snappy, lz4 and zstd
  serving broker (`--kafka-fetch-compression`) read back to the same value; a wrong password is exit 67 over both schemes.
- Port finding (not a Surl defect, ADR-0083): the port's Metadata v4 does not set `allow_auto_topic_creation`, so a produce to
  a topic nobody declared gets `UNKNOWN_TOPIC_OR_PARTITION` (exit 8). The tests declare the topic with `--topic`
  (ADR-0105 decision 2 says Metadata v4 without the flag answers 3).
- The compressed-batch test cannot see the codec on the wire from the client's output; that the served batch names the codec
  is proved by `KafkaFetchAnswererTests.ServeAsync_EachServedCodec_...`, the live test proves the client decodes it.
- Matrix: the rows left on BL-517, BL-520 and BL-521 now name their unit tests (found by name in the test projects); the TLS
  row (`Kafka:Guide-Tls-1`) and `Kafka:Guide-Sasl-5` also name the live tests.

## Log

- 2026-11-04: Created.
- 2026-11-06: depends-on BL-522 replaced by BL-973 (BL-522 split for the 60-minute size limit; Stewart asked for a backlog audit 2026-11-06).
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Backlog. Waits on BL-974: the compressed-batch exchange needs surl's --kafka-fetch-compression, which BL-974 adds (Fetch serves uncompressed until then)
- 2026-11-12: Backlog -> Doing.
- 2026-11-12: Doing -> Done. pinned Curl port kafka client produces, consumes (plain, TLS, compressed batches) and is refused a bad password against live surl; Kafka matrix fully covered
