---
id: BL-699
title: Encode zstd frames in Surl.Compression
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-516, BL-698]
touches: [Foundation/Surl.Compression.UnitLibrary, Foundation/Surl.Compression.UnitTests]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-699 — Encode zstd frames in Surl.Compression

## Goal

`Surl.Compression` holds a hand-built `ZstdFrameEncoder` that writes one RFC 8878 frame with `Frame_Content_Size`,
each block a Compressed block of raw literals and Predefined_Mode sequences from a greedy matcher with repeat
offsets, a Raw block when that is not smaller, or an RLE block for a run of one byte, so the Kafka broker can serve
zstd batches under `--kafka-fetch-compression zstd`.

## Context

- Decision: ADR-0105 decision 4 (the encoder's shape: no checksum, no dictionary).
- Specification: RFC 8878 sections 3.1.1 (frame header and block types), 3.1.1.3 (Raw literals, the sequences
  section header, Predefined_Mode), 3.1.1.5 (repeat offsets) and 4.1 (FSE encoding with the predefined
  distributions of the RFC's appendix).
- BL-698's decoder proves the round trip; its predefined tables may be shared.

## Acceptance criteria

- [x] Round-trip tests show the encoder's output decoded by `ZstdFrameDecoder` back to the input for empty input,
      one byte, a run of one byte (an RLE block), incompressible input (a Raw block), repetitive text (a Compressed
      block smaller than the input), and input past one block's 128 KiB.
- [x] A named test pins one small frame's bytes, worked out by hand from RFC 8878 in the test's comments.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-516 (ADR-0105 decision 4).
- Delivered: public `ZstdFrameEncoder.Encode`; internal `ZstdSequenceFinder`, `ZstdSequencesEncoder`,
  `ZstdFiniteStateEntropyEncodingTable`, `ZstdBackwardBitWriter`, `ZstdSequence`, `ZstdBlockSequences`, and
  `ZstdRepeatOffsets`, which the decoder's `ZstdSequencesSection` now uses too (its private repeat-offset code
  moved there, and its code tables became internal for the encoder). No new ADR: ADR-0105 decision 4 fixes the
  encoder's shape, and the defaults below are choices inside it.
- Defaults taken: a single-segment frame (no window descriptor, the window being the content size) with
  Frame_Content_Size in 1, 2 or 4 bytes; RLE only for a block of two or more equal bytes (one byte is no smaller
  as RLE); a repeat-offset match preferred when at least as long as the hash table's; matches reach back across
  blocks, capped at 2^29 - 4 so that offset code 28, the predefined table's highest, names every offset; a block
  kept Raw or RLE leaves the repeat offsets untouched, as it leaves a decoder's.
- Pinned frame: `ZstdFrameEncoderTests.Encode_AbcdFourTimes_WritesTheFrameWorkedOutFromRfc8878` (Appendix A
  states LL 4, OF 23, ML 28; repeat code 2).
- Surl.Compression.UnitLibrary measured with the MSTest coverage collector: 100% line, 100% branch. Fast tests
  green across the solution (Surl.Compression.UnitTests: 216).

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. ZstdFrameEncoder writes RFC 8878 frames (RLE, Raw, Compressed with Predefined_Mode sequences and repeat offsets) that ZstdFrameDecoder round-trips
