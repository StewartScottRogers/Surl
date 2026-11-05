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
completed:
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

- [ ] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [ ] Named tests show:
      - each primitive type round-tripped at its boundaries (VARINT zigzag of -1, 0, 1, int.MaxValue,
        int.MinValue; a null NULLABLE_STRING as length -1);
      - a request split across reads reassembled;
      - the correlation id echoed;
      - a size prefix over the limit closing the connection;
      - an unknown API key answered as the ADR says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
