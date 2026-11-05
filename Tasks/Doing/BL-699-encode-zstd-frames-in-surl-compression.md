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
completed:
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

- [ ] Round-trip tests show the encoder's output decoded by `ZstdFrameDecoder` back to the input for empty input,
      one byte, a run of one byte (an RLE block), incompressible input (a Raw block), repetitive text (a Compressed
      block smaller than the input), and input past one block's 128 KiB.
- [ ] A named test pins one small frame's bytes, worked out by hand from RFC 8878 in the test's comments.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Filed by BL-516 (ADR-0105 decision 4).

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
