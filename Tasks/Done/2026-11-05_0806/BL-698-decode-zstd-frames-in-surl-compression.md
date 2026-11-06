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
completed: 2026-11-05
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

- [x] Named tests decode a frame of one Raw block, Raw and RLE blocks, a Compressed block with raw literals and
      RLE-mode sequences using repeat offsets, Huffman literals in one and four streams with direct and
      FSE-compressed weights, Treeless literals, and sequences in Predefined, FSE_Compressed and Repeat modes,
      mixed in one block.
- [x] Named tests skip a skippable frame, check a content checksum (a wrong one refused), and refuse a wrong magic,
      a dictionary id, Treeless literals or Repeat mode with no earlier table, and output past the bound.
- [x] `XxHash64` matches the published vectors.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage with every method at complexity 10 or less.

## Notes

- Filed by BL-516 (ADR-0105 decision 4). Too large to share a run with BL-702 or BL-699.
- Delivered: `ZstdFrameDecoder` (public), `XxHash64` (public), and internal `ZstdLiteralsSection`,
  `ZstdSequencesSection`, `ZstdHuffmanTable`, `ZstdFiniteStateEntropyTable`, `ZstdBackwardBitReader`,
  `LittleEndianValue`; `BoundedDecompressionOutput.Remaining`. Structure copied from the Curl port's
  Kafka zstd decoder (ADR-0083 allows code), each rule re-checked against RFC 8878; no port test or
  fixture used.
- Tests: frames are built by test-side writers following the RFC (`ZstdTestFrames`,
  `ZstdHuffmanLiteralsWriter`, `ZstdFseDescriptionWriter`, `ZstdSequencesWriter`,
  `ZstdBackwardBitStreamWriter`, `ZstdStateChains`). The FSE state chains use the library's table,
  which `ZstdFiniteStateEntropyTableTests` pins to RFC Appendix A rows for all three predefined tables;
  Huffman codes are pinned to the RFC 4.2.1 example. XXH64 vectors: the published ones plus a 13-byte
  input (8-, 4- and 1-byte tails), all cross-checked with Microsoft's System.IO.Hashing.XxHash64 loaded
  from the SDK's Android pack in PowerShell (a check only, no reference added).
- Choices (defaults, within ADR-0105 decision 4): every frame in the input is decoded and joined (RFC 8878
  section 3: data is one or more frames); a match never reaches into an earlier frame; a Dictionary_ID of 0
  means no dictionary (section 3.1.1.1.3) and only a non-zero one is refused; the window size is not
  enforced, the output bound is; literals over Block_Maximum_Size (128 KiB) and bytes after a
  zero-sequence section are refused as malformed, as the reference decoder does; empty input is
  malformed.
- Gates: Surl.Compression.UnitLibrary 100% line, 100% branch, worst CRAP 10 (Measure-CodeQuality.ps1).
  Splitting `Lz4FrameDecoder.ReadBlock` (WriteBlock extracted) brought the existing LZ4 method from 12 to
  under 10 on the script's count, so the whole library passes.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. ZstdFrameDecoder decodes RFC 8878 frames (Raw/RLE/Compressed blocks, Huffman and FSE, all sequence modes) with XxHash64 checksums
