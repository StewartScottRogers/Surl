# Surl.Qpack.UnitLibrary

Phase 6.

QPACK, the field compression of HTTP/3 (RFC 9204), hand-built because the base class library has
no public QPACK (ADR-0082 decisions 2 and 8):

- `QpackDecoder` - applies the peer's encoder stream to its dynamic table
  (`ReceiveEncoderStream`), decodes field sections (`DecodeFieldSection`) and writes the decoder
  stream's Section Acknowledgments, Stream Cancellations and Insert Count Increments
  (`TakeDecoderStreamInstructions`). It refuses a capacity above the
  `SETTINGS_QPACK_MAX_TABLE_CAPACITY` it was given and an entry larger than the capacity
  (`QPACK_ENCODER_STREAM_ERROR`), a reference outside the table
  (`QPACK_DECOMPRESSION_FAILED`), and a field section larger than the
  `SETTINGS_MAX_FIELD_SECTION_SIZE` it was given (`--max-message`, ADR-0082 decision 7; its
  stream alone is answered). It holds no blocked streams: the server advertises
  `SETTINGS_QPACK_BLOCKED_STREAMS` 0. Surl's server gives it a maximum capacity of 0; the RFC's
  Appendix B examples give it 220.
- `QpackEncoder` - encodes field sections with the static table and literals only, Huffman-coding
  strings as its `HpackStringCoding` says, and reads the peer's decoder stream.
- `QpackFieldLine`, `QpackFieldSectionResult`, `QpackInstructionResult`, `QpackDecodingError`,
  `QpackErrorCode` - public. Nothing throws on malformed input: a result names the
  `QpackDecodingError` and the `QpackErrorCode` that ends the connection.
- `QpackStaticTable` (RFC 9204 Appendix A, not HPACK's), `QpackDynamicTable`,
  `QpackFieldSectionReader`, `QpackInstructionStream`, `QpackPrimitives` and
  `QpackDecodingException` - internal, visible to `Surl.Qpack.UnitTests` only. The exception never
  leaves the library.

Names and values are byte strings carried as Latin-1 `string`s: one character per byte.

Namespace `Surl.Qpack`. This library references `Surl.Hpack` only, for HPACK's Huffman code and
`HpackStringCoding` (ADR-0082 decision 2, amending ADR-0002's table): no
`Surl.Protocol.Abstractions`, no I/O, no clock.

`Surl.Qpack.UnitTests` replays every example of RFC 9204 Appendix B; the bytes come from the RFC,
never from the Curl port (ADR-0003).
