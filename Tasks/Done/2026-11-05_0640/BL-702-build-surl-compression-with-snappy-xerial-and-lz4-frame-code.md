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
completed: 2026-11-05
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

- [x] The two projects exist in `Foundation/`, are in `Surl.slnx`, reference nothing, and the ADR-0002 table test
      passes with the `Surl.Compression.UnitLibrary` row.
- [x] Named tests decode Snappy literals and each copy form (including an overlapping copy), refuse offset 0, an
      offset before the output's start and a short output, and decode xerial-framed input of two chunks.
- [x] Named tests decode an LZ4 frame with compressed blocks, stored blocks, linked blocks, block and content
      checksums (checked), refuse a wrong magic, offset 0 and a bad checksum.
- [x] Round-trip tests show `SnappyEncoder` (in xerial framing) and `Lz4FrameEncoder` output decoded back to the
      input, and that each encoder's output is smaller than a repetitive input.
- [x] `XxHash32` matches the published vectors.
- [x] Each decoder stops with its bound outcome when output would pass the bound.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line and branch
      coverage.

## Notes

- Filed by BL-516 (ADR-0105 decision 4). BL-698 and BL-699 add zstd to the library this task creates.
- Shape (2026-11-05): every decoder is `Decode(input, outputBound)` returning a `DecompressionResult` with a
  `DecompressionOutcome` of `Decompressed`, `Malformed`, `ChecksumMismatch` or `OutputBoundPassed`; an
  internal `DecompressionRefusedException` stops decoding and is caught by each public method, so nothing
  escapes. `BoundedDecompressionOutput` holds the bound and the LZ77 copy both formats share;
  `GreedyMatchFinder` (14-bit hash table) is the matcher both encoders share. BL-698 can reuse both.
- Defaults taken, within ADR-0105 decision 4: Snappy decode refuses a declared length past the bound before
  decoding, and output longer or shorter than declared as malformed. `XerialSnappyFraming.DecodeFramedOrRaw`
  is where "the magic chooses"; the xerial version fields are read but not checked (snappy-java writes 1/1).
  The LZ4 decoder checks the header, block and content checksums, refuses the version other than 01, a
  reserved bit, a dictionary id (no dictionary to decode against), a block-maximum code below 4, a block or
  decoded block past the block maximum, a content size that disagrees, an independent block copying from an
  earlier block, and bytes after the frame. XXH32 is seed 0 only, which is all LZ4 uses.
- Snappy's encoder uses an unbounded window (4-byte offsets past 65535), which the format allows; the LZ4
  encoder's matches stay within each 64 KiB block, so offsets never pass 65535.
- Vectors: XXH32 "", "a", "abc", "Nobody inspects the spammish repetition"; LZ4 header checksum bytes 0xA7
  (FLG 0x64) and 0x82 (FLG 0x60); the lz4 tool's empty frame `04 22 4D 18 64 40 A7 00000000 055DCC02`.
- Coverage measured with the MSTest coverage collector: 100% line and 100% branch for the library.
- Not touched (outside `touches`): `Documentation/Product/Product-Overview.md` does not list the new
  library yet; the align-and-document pass that lands with BL-521 can add it.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Compression decodes and encodes raw and xerial Snappy and LZ4 frames with checked XXH32 checksums and a typed output bound
