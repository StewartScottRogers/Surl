# Surl.Compression.UnitLibrary

The hand-built codecs and checksums any server may use (ADR-0105, decision 4, which adds this
library to ADR-0002 decision 3's table). It references nothing.

- `SnappyDecoder` and `SnappyEncoder`: raw Snappy (google/snappy `format_description.txt`).
- `XerialSnappyFraming`: snappy-java's xerial stream framing, which Kafka's Java producer
  writes; `DecodeFramedOrRaw` lets the magic choose between it and raw Snappy.
- `Lz4FrameDecoder` and `Lz4FrameEncoder`: one LZ4 frame (lz4 `doc/lz4_Frame_format.md`,
  `doc/lz4_Block_format.md`), every checksum present checked on decode.
- `XxHash32`: XXH32 with seed 0, for the LZ4 checksums.
- `ZstdFrameDecoder`: zstd (RFC 8878) - every frame in the input, contents joined, skippable
  frames skipped, a non-zero dictionary id refused, the content checksum checked when present.
  Its parts are internal: `ZstdLiteralsSection` (Raw, RLE, Huffman in one or four streams,
  Treeless), `ZstdSequencesSection` (Predefined, RLE, FSE_Compressed and Repeat tables, the repeat
  offsets), `ZstdHuffmanTable`, `ZstdFiniteStateEntropyTable` and `ZstdBackwardBitReader`. A match
  never reaches into an earlier frame; the window size is not enforced, the output bound is.
- `XxHash64`: XXH64 with seed 0; a zstd content checksum is its lowest 32 bits.

Rules:

- Every decoder takes the most bytes it may write and returns a `DecompressionResult`; past
  the bound it is `OutputBoundPassed`, and malformed input is `Malformed` or
  `ChecksumMismatch`, never an exception out of the library. `DecompressionRefusedException`
  is internal and is caught by each decoder's public method.
- Expected bytes in the tests come from the format documents and their published vectors,
  never from the Curl port (ADR-0003, ADR-0083).
- zstd test frames are written by the test project's `ZstdTestFrames`, `ZstdHuffmanLiteralsWriter`,
  `ZstdFseDescriptionWriter` and `ZstdSequencesWriter`, each following the RFC section it names;
  the predefined FSE tables are pinned to the RFC's Appendix A.
- `ZstdFrameEncoder` is still to come (BL-699).
