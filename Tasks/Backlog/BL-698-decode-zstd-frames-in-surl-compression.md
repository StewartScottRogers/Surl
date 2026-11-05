---
id: BL-698
title: Decode zstd frames in Surl.Compression
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-516, BL-702]
touches: [Foundation/Surl.Compression.UnitLibrary, Foundation/Surl.Compression.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-698 — Decode zstd frames in Surl.Compression

## Goal

`Surl.Compression` holds a hand-built `ZstdFrameDecoder`, with its Huffman and FSE decoders and `XxHash64`, that
decodes any RFC 8878 frame without a dictionary, so the Kafka broker can store zstd-compressed batches a producer
sends.

## Context

- Decision: ADR-0105 decision 4 (skippable frames skipped, a dictionary id refused, the content checksum checked
  when present, the output bound).
- Specification: RFC 8878 sections 3.1.1 (frame header, Raw, RLE and Compressed blocks), 3.1.1.3 (literals:
  Raw, RLE, Compressed and Treeless, one and four streams, direct and FSE-compressed Huffman weights; sequences:
  Predefined, RLE, FSE_Compressed and Repeat modes), 3.1.1.4 and 3.1.1.5 (sequence execution, repeat offsets),
  3.1.2 (skippable frames), 4 (FSE and Huffman), and the XXH64 specification with its published vectors. The
  RFC's appendix tables give the predefined distributions.
- Library from BL-702. Copying the Curl port's decoder code is allowed (ADR-0083); expected output comes from
  frames built from the RFC, never from the port.

## Acceptance criteria

- [ ] Named tests decode a frame of one Raw block, Raw and RLE blocks, a Compressed block with raw literals and
      RLE-mode sequences using repeat offsets, Huffman literals in one and four streams with direct and
      FSE-compressed weights, Treeless literals, and sequences in Predefined, FSE_Compressed and Repeat modes,
      mixed in one block.
- [ ] Named tests skip a skippable frame, check a content checksum (a wrong one refused), and refuse a wrong magic,
      a dictionary id, Treeless literals or Repeat mode with no earlier table, and output past the bound.
- [ ] `XxHash64` matches the published vectors.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage with every method at complexity 10 or less.

## Notes

- Filed by BL-516 (ADR-0105 decision 4). Too large to share a run with BL-702 or BL-699.

## Log

- 2026-11-05: Created.
