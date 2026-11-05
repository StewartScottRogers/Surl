---
id: BL-442
title: Create the gRPC server with its message framing and protobuf wire codec
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-441, BL-404]
touches: [Protocols/Http/Surl.Extended.Protocol.Grpc.UnitLibrary, Protocols/Http/Surl.Extended.Protocol.Grpc.UnitTests, Surl.slnx, Documentation/Conformance/Extended/Grpc.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
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

- [x] Both projects exist, are in `Surl.slnx`, and `ProtocolIsolationTests` passes.
- [x] Named tests show:
      - the protobuf encoding guide's worked examples (`08 96 01` for field 1 = 150, the string example, the
        embedded-message example, and the packed example) encoded and decoded byte for byte;
      - a length-prefixed message split across reads is reassembled;
      - a gzip-flagged message is decompressed;
      - a message over the limit gives `RESOURCE_EXHAUSTED`;
      - each `grpc-timeout` unit parsed, and a malformed one refused as the ADR says.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

- Delivered (2026-11-05): `LengthPrefixedMessage`, `LengthPrefixedMessageReader`, `GrpcMessageEncoding`,
  `GrpcMessageDecompression`, `GrpcTimeout`, `GrpcStatusCode`, `GrpcStatus`, `GrpcStatusMessage`,
  `GrpcCallException`, and the protobuf codec `ProtobufReader`, `ProtobufWriter`, `ProtobufField`,
  `ProtobufWireType`, `ProtobufZigZag`, `ProtobufFormatException`. 134 tests; the library measured at line
  rate 1 and branch rate 1 with `dotnet test --collect "Code Coverage;Format=cobertura"`.
- Criteria to tests: guide examples `ProtobufEncodingGuideExampleTests` (each example written and read);
  split message `LengthPrefixedMessageReaderTests.TryRead_MessageSplitAcrossReadsByteByByte_ReassemblesIt`;
  gzip `GrpcMessageDecompressionTests.ReadPayload_GzipFlaggedMessage_IsDecompressed`; over the limit
  `LengthPrefixedMessageReaderTests.TryRead_MessageLengthOverTheLimit_ThrowsResourceExhaustedFromThePrefixAlone`
  and `GrpcMessageDecompressionTests.ReadPayload_DecompressedPastTheLimit_ThrowsResourceExhausted`;
  `grpc-timeout` `GrpcTimeoutTests.Read_EachUnit_IsReadAtItsScale` and
  `Read_MalformedValue_ThrowsInternalMalformedGrpcTimeout`.
- Choices within ADR-0116 (sensible defaults, no new ADR needed):
  - A Compressed-Flag other than 0 or 1, and a body ending inside a message, are `INTERNAL`
    `request message did not parse`: the matrix row Grpc:HTTP2-Message-2 calls it a message that does not
    parse, and ADR-0116 decision 2 gives that text.
  - The `--max-filesize` call total is counted in `LengthPrefixedMessageReader` too (it reads every message
    of a call, so it is the one place that sees them all); BL-443 only passes the limit.
  - `grpc-timeout` in nanoseconds rounds up to the 100 ns tick, so `1n` is a 1-tick deadline rather than 0
    (which ADR-0116 reserves for "already passed"); hours up to 99999999H fit a `TimeSpan`.
  - Groups (wire types 3 and 4) are read only to skip or keep them as unknown fields, nested at most 100
    deep (protobuf's own recursion limit), so a hostile message cannot overflow the stack.
  - The protobuf codec was written here from the encoding guide; only `GrpcStatusCode`'s member list and
    `LengthPrefixedMessageReader`'s buffering shape were taken from the Curl port's code (code, not a
    verdict; none of its tests or bytes).
- `touches` grew by `Documentation/Conformance/Extended/Grpc.md`: ADR-0116 says each task replaces its ID in
  the matrix with the named test. No task in Doing names it.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Extended.Protocol.Grpc exists: gRPC message framing, gzip/deflate decompression, grpc-timeout parsing, the status map and a hand-written protobuf codec, 134 tests at 100% coverage
