---
id: BL-442
title: Create the gRPC server with its message framing and protobuf wire codec
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-441, BL-404]
touches: [Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests, Surl.slnx]
requirement: none
created: 2026-11-04
completed:
---
# BL-442 — Create the gRPC server with its message framing and protobuf wire codec

## Goal

`Surl.Extended.Protocol.Grpc.UnitLibrary` exists. It reads and writes gRPC length-prefixed messages (with the
compressed flag), encodes and decodes the protobuf wire format the built-in service and reflection need, parses
`grpc-timeout`, and maps every failure to its status code. None of this needs HTTP/2 yet.

## Context

- Decision: BL-441's ADR, decisions 1, 4, 5 and 6. Rules: BL-543's ADR.
- New projects in `Protocols/Http/`, listed in `Surl.slnx` under `/Protocols/Http/`. Namespace
  `Surl.Extended.Protocol.Grpc`. Reference `Surl.Protocol.Abstractions` only for now; BL-443 adds the
  HTTP/2 library.
- PROTOCOL-HTTP2.md "Length-Prefixed-Message" (1-byte compressed flag, 4-byte big-endian length) and `grpc-timeout`
  (`TimeoutValue` of at most 8 digits, units `H M S m u n`). Protobuf encoding guide: varints, zigzag, the
  wire types, packed repeated fields, and unknown fields kept.
- No protobuf package: the codec is hand-written (root `CLAUDE.md`, "Base class library only"). The port's codec
  may be copied (code is not a verdict); its tests may not.

## Acceptance criteria

- [ ] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [ ] Named tests show:
      - the protobuf encoding guide's worked examples (`08 96 01` for field 1 = 150, the string example, the
        embedded-message example, and the packed example) encoded and decoded byte for byte;
      - a length-prefixed message split across reads is reassembled;
      - a gzip-flagged message is decompressed;
      - a message over the limit gives `RESOURCE_EXHAUSTED`;
      - each `grpc-timeout` unit parsed, and a malformed one refused as the ADR says.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
