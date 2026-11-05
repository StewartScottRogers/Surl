# Surl.Compression.UnitLibrary

The hand-built codecs and checksums any server may use (ADR-0105, decision 4, which adds this
library to ADR-0002 decision 3's table). It references nothing.

- `SnappyDecoder` and `SnappyEncoder`: raw Snappy (google/snappy `format_description.txt`).
- `XerialSnappyFraming`: snappy-java's xerial stream framing, which Kafka's Java producer
  writes; `DecodeFramedOrRaw` lets the magic choose between it and raw Snappy.
- `Lz4FrameDecoder` and `Lz4FrameEncoder`: one LZ4 frame (lz4 `doc/lz4_Frame_format.md`,
  `doc/lz4_Block_format.md`), every checksum present checked on decode.
- `XxHash32`: XXH32 with seed 0, for the LZ4 checksums.

Rules:

- Every decoder takes the most bytes it may write and returns a `DecompressionResult`; past
  the bound it is `OutputBoundPassed`, and malformed input is `Malformed` or
  `ChecksumMismatch`, never an exception out of the library. `DecompressionRefusedException`
  is internal and is caught by each decoder's public method.
- Expected bytes in the tests come from the format documents and their published vectors,
  never from the Curl port (ADR-0003, ADR-0083).
- zstd (`ZstdFrameDecoder`, `XxHash64`, `ZstdFrameEncoder`) is still to come (BL-698, BL-699).
