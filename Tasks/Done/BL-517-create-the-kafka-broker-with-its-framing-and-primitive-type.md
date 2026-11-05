---
id: BL-517
title: Create the Kafka broker with its framing and primitive-type codec
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-516, BL-404]
touches: [Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitLibrary, Protocols/Messaging/Surl.Extended.Protocol.Kafka.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-517 — Create the Kafka broker with its framing and primitive-type codec

## Goal

`Surl.Extended.Protocol.Kafka.UnitLibrary` exists. It reads size-prefixed requests and their request header
(v0 to v2, as the APIs served need), writes response headers, encodes every primitive type the served schemas
use, and answers an unknown API key or version as BL-516's ADR says.

## Context

- Decision: BL-516's ADR, decisions 1 and 7. Rules: BL-543's ADR.
- New projects in `Protocols/Messaging/`, listed in `Surl.slnx`. Namespace `Surl.Extended.Protocol.Kafka`.
  References: `Surl.Protocol.Abstractions` only.
- Kafka protocol guide:
  - "Protocol Primitive Types" (BOOLEAN, INT8 to INT64, UINT32, VARINT, VARLONG, UUID, FLOAT64, STRING,
    NULLABLE_STRING, BYTES, NULLABLE_BYTES, RECORDS, ARRAY, and the COMPACT forms with tagged fields if a
    flexible version is served);
  - "Common Request and Response Structure" (the size prefix, request header versions, correlation id echoed);
  - the error-codes table.

## Acceptance criteria

- [x] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [x] Named tests show:
      - each primitive type round-tripped at its boundaries (VARINT zigzag of -1, 0, 1, int.MaxValue,
        int.MinValue; a null NULLABLE_STRING as length -1);
      - a request split across reads reassembled;
      - the correlation id echoed;
      - a size prefix over the limit closing the connection;
      - an unknown API key answered as the ADR says.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

- Delivered (2026-11-05): `KafkaPrimitiveReader`/`KafkaPrimitiveWriter` (every primitive type, compact
  forms, tagged fields), `KafkaRequestFrameReader`, `KafkaRequestHeader` (v0 to v2), `KafkaResponseFrame`
  (response header v0 and v1), `KafkaServedApis`/`KafkaApiVersionRange` (ADR-0105 decision 1's table),
  `KafkaApiVersionsFallback`, `KafkaBroker` (`kafka`, `kafkas`) and `KafkaBrokerConnection`.
- Named tests: `KafkaPrimitiveReaderTests` (`VarInt_RoundTripsZigzagAtItsBoundaries` for -1, 0, 1,
  int.MaxValue, int.MinValue; `NullableString_Null_IsLengthMinusOne`; every other type's round trip),
  `KafkaRequestFrameReaderTests.ReadRequestAsync_RequestSplitAcrossReads_IsReassembled`,
  `KafkaBrokerTests.ServeAsync_ServedRequest_EchoesTheCorrelationIdUnderResponseHeaderZero`,
  `KafkaBrokerTests.ServeAsync_SizePrefixOverTheRequestLimit_ClosesWithNoBytes`,
  `KafkaBrokerTests.ServeAsync_UnknownApiKey_ClosesWithNoBytes`, and
  `KafkaBrokerTests.ServeAsync_ApiVersionsAboveItsRange_AnswersVersionZeroWithUnsupportedVersionAndEveryRange`.
- Coverage measured with `Measure-CodeQuality.ps1 -SkipTestRun` over this project's run: 100% line,
  100% branch, highest cyclomatic complexity 10, no member failing.
- Choice: served APIs are answered through `IKafkaApiAnswerer`, one per API, passed to `KafkaBroker`'s
  constructor, so BL-518 to BL-521 each add an answerer without touching the framing. A served API with
  no answerer yet closes the connection with no bytes and the note "<Api> is not answered yet", the same
  answer ADR-0105 gives an unserved API.
- Choice: the ApiVersions fallback for a version above 3 (ADR-0105 decision 1) is built here, since it
  is the answer to a version outside the ranges; BL-518 answers the in-range versions.
- Choice: the head timeout runs until the first request is complete, not only its size prefix, as the
  MQTT 5 broker does: a stricter reading of ADR-0105 decision 7's row that also stops a client
  trickling one request's body. The login part of that row is BL-518's.
- Choice: an array's element count above the bytes left is malformed, so a forged count cannot make an
  answerer allocate more than the request holds (every element of the served schemas takes at least
  one byte).
- No ADR needed: every behaviour follows ADR-0105 as written.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Kafka broker reads size-prefixed requests and headers v0-v2, encodes every primitive type, echoes correlation ids, and closes or falls back for APIs outside ADR-0105's ranges
