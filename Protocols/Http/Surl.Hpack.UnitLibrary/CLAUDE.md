# Surl.Hpack.UnitLibrary

Phase 6.

HPACK, the header compression of HTTP/2 (RFC 7541), hand-built because the base class library
has no public HPACK (ADR-0081 decision 4):

- `HpackDecoder` - decodes header blocks into header lists. It refuses a dynamic table size
  update above the `SETTINGS_HEADER_TABLE_SIZE` it was given and a header list larger than the
  `SETTINGS_MAX_HEADER_LIST_SIZE` it was given (`--max-message`, ADR-0081 decision 5). `Decode`
  never throws on malformed input: it returns an `HpackDecodeResult` whose `Error` names the
  `HpackDecodingError`.
- `HpackEncoder` - encodes header lists into header blocks, choosing the representations RFC 7541
  Appendix C does, Huffman-coding strings as its `HpackStringCoding` says, and honouring the peer's
  `SETTINGS_HEADER_TABLE_SIZE` through `SetPeerMaximumTableSize`.
- `HpackHuffman` - the Huffman code of RFC 7541 Appendix B, public because QPACK (BL-382,
  `Surl.Qpack`) reuses it.
- `HpackHeaderField`, `HpackFieldIndexing`, `HpackDecodeResult`, `HpackDecodingError`,
  `HpackStringCoding` - public.
- `HpackStaticTable`, `HpackDynamicTable`, `HpackPrimitives` and `HpackDecodingException` -
  internal, visible to `Surl.Hpack.UnitTests` only. The exception never leaves the library.

Names and values are byte strings carried as Latin-1 `string`s: one character per byte.

Namespace `Surl.Hpack`. This library references nothing (ADR-0081 decision 4, amending ADR-0002's
table): no `Surl.Protocol.Abstractions`, no I/O, no clock. `Surl.Http2` (ADR-0086) and
`Surl.Qpack` (ADR-0082) reference it.

`Surl.Hpack.UnitTests` replays every example of RFC 7541 Appendix C (C.1 to C.6) both ways; the
bytes come from the RFC, never from the Curl port (ADR-0003).
