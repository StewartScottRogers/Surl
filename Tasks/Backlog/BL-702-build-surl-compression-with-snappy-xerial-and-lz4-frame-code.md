---
id: BL-702
title: Build Surl.Compression with Snappy, xerial and LZ4 frame codecs and XXH32
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-516]
touches: [Foundation/Surl.Compression.UnitLibrary, Foundation/Surl.Compression.UnitTests, Surl.slnx, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-702 — Build Surl.Compression with Snappy, xerial and LZ4 frame codecs and XXH32

## Goal

`Foundation/Surl.Compression.UnitLibrary` exists with its `.UnitTests` twin, holding hand-built `SnappyDecoder`,
`SnappyEncoder`, `XerialSnappyFraming`, `Lz4FrameDecoder`, `Lz4FrameEncoder` and `XxHash32`, which the Kafka broker
uses to decode produced batches and to compress the batches it serves.

## Context

- Decision: ADR-0105 decision 4 (the codecs, their encoder shapes and the output bound) and its amendment of
  ADR-0002 decision 3's table (`Surl.Compression.UnitLibrary`, referencing nothing).
- Specifications: google/snappy `format_description.txt` (literals, copies with 1-, 2- and 4-byte offsets, the
  varint length preamble); snappy-java's xerial stream framing (magic `0x82 "SNAPPY" 0x00`, big-endian INT32
  version and compatible version, then chunks of a big-endian INT32 length and raw Snappy); lz4/lz4
  `doc/lz4_Frame_format.md` and `doc/lz4_Block_format.md`; the XXH32 specification (xxHash `doc/xxhash_spec.md`)
  with its published test vectors.
- New projects in `Foundation/`, listed in `Surl.slnx` under `/Foundation/` in ordinal order, namespace
  `Surl.Compression`, no package. `Surl.Protocol.Abstractions.UnitTests`' horizontal-library table test learns the
  ADR-0105 row once the project exists.
- Each decoder takes the most bytes it may write and stops with a typed outcome past it (ADR-0105's decompression
  bound); malformed input is a typed outcome, never an unhandled exception.
- Copying code from the Curl port's `Curl.Extended.Protocol.Kafka.UnitLibrary` decoders or its reference encoders is
  allowed (ADR-0083); expected bytes come from the format documents and their vectors, never from the port.

## Acceptance criteria

- [ ] The two projects exist in `Foundation/`, are in `Surl.slnx`, reference nothing, and the ADR-0002 table test
      passes with the `Surl.Compression.UnitLibrary` row.
- [ ] Named tests decode Snappy literals and each copy form (including an overlapping copy), refuse offset 0, an
      offset before the output's start and a short output, and decode xerial-framed input of two chunks.
- [ ] Named tests decode an LZ4 frame with compressed blocks, stored blocks, linked blocks, block and content
      checksums (checked), refuse a wrong magic, offset 0 and a bad checksum.
- [ ] Round-trip tests show `SnappyEncoder` (in xerial framing) and `Lz4FrameEncoder` output decoded back to the
      input, and that each encoder's output is smaller than a repetitive input.
- [ ] `XxHash32` matches the published vectors.
- [ ] Each decoder stops with its bound outcome when output would pass the bound.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

- Filed by BL-516 (ADR-0105 decision 4). BL-698 and BL-699 add zstd to the library this task creates.

## Log

- 2026-11-05: Created.
