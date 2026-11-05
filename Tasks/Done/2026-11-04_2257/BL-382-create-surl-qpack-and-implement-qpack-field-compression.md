---
id: BL-382
title: Create Surl.Qpack and implement QPACK field compression
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-365, BL-372]
touches: [Surl.slnx, Surl.Qpack.UnitLibrary, Surl.Qpack.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-382 — Create Surl.Qpack and implement QPACK field compression

## Goal

`Surl.Qpack.UnitLibrary` and `Surl.Qpack.UnitTests` exist and are listed in `Surl.slnx`. The library
decodes the field sections that upstream curl sends and encodes the server's (RFC 9204), with the
dynamic-table behaviour that BL-372's ADR decides, and reuses `Surl.Hpack`'s Huffman code.

## Context

- Decision: BL-372's ADR, decisions 2 and 8. `Surl.Qpack` is the planning name. Its reference to
  `Surl.Hpack` (BL-365) needs the ADR's ADR-0002 row, in the way `Surl.Cryptography.Ed25519` references
  `Surl.Cryptography.Curve25519`.
- To create the project, copy BL-292 (commit `5008a375`): the csproj pair, `CLAUDE.md`, ordinal
  `Surl.slnx` lines, and the `ProtocolIsolationTests` row.
- RFC 9204: section 3 (static and dynamic tables), 4.3 and 4.4 (encoder and decoder stream
  instructions), 4.5 (field line representations, Required Insert Count and Base), and Appendix A
  (the static table, which differs from HPACK's). Appendix B has worked examples.
- The decoder is bounded by the header-block limit that the ADR maps from `--max-message` (ADR-0006).

## Acceptance criteria

- [x] Both projects exist and are listed in `Surl.slnx` in ordinal order. The production csproj has no
      `PackageReference`. `ProtocolIsolationTests` knows the row and passes.
- [x] Every example in RFC 9204 Appendix B decodes to the RFC's field lines and table state, and the
      encoder reproduces the RFC's bytes for the representations the ADR has the server use.
- [x] With the dynamic table capacity the ADR advertises, a field section that references an entry
      outside it fails with `QPACK_DECOMPRESSION_FAILED`, and an encoder-stream instruction that
      overflows the table fails with `QPACK_ENCODER_STREAM_ERROR`. Each has a named test.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line
      and branch coverage.

## Notes

- Built: `Protocols/Http/Surl.Qpack.UnitLibrary` (references `Surl.Hpack` only, for `HpackHuffman`
  and `HpackStringCoding`) and its `.UnitTests` twin, between `Surl.Protocol.Ws` and `Surl.Quic` in
  `Surl.slnx`; `ProtocolIsolationTests` has the `[Qpack] = [Hpack]` row and its allowed/forbidden
  data rows. 99 tests; 100% line and branch coverage, every method at complexity 10 or less
  (Cobertura from the MSTest coverage collector).
- Named tests for criterion 3 (capacity 0, ADR-0082 decision 8):
  `QpackDecoderTests.DecodeFieldSection_ServerCapacityAndDynamicReference_FailsWithDecompressionFailed`,
  `..._ServerCapacityAndNonZeroRequiredInsertCount_FailsWithDecompressionFailed`,
  `ReceiveEncoderStream_ServerCapacityAndInsert_FailsWithEncoderStreamError` and
  `ReceiveEncoderStream_ServerCapacityAndCapacityAboveZero_FailsWithEncoderStreamError`.
  Appendix B: `QpackAppendixBTests` (B.1 both ways, B.2 to B.5 decoded with the RFC's tables).
- Choices (sensible defaults, within ADR-0082 decision 8, so no new ADR):
  - The decoder implements the whole dynamic table, bounded by the maximum capacity it is given, so
    Appendix B replays as written; Surl's server gives it 0. It holds no blocked streams (the server
    sends `SETTINGS_QPACK_BLOCKED_STREAMS` 0), so a section needing inserts not yet received fails
    with `QPACK_DECOMPRESSION_FAILED`, as RFC 9204 section 2.1.2 says. It also refuses a Required
    Insert Count above what the section references (the MAY of section 2.2.1).
  - `--max-message` bounds the decoded field section (`FieldSectionTooLarge`); that result carries no
    connection error code, because ADR-0082 decision 7 answers it on its stream alone (431).
  - Encoder-stream string literals longer than the capacity could hold are refused from their length
    prefix, before their bytes are buffered (Huffman literals allowed 4x, the code's worst ratio),
    so the encoder stream's buffer stays bounded.
  - Insert Count Increments are coalesced and written after the queued Section Acknowledgments and
    Stream Cancellations when the caller takes the decoder stream's bytes; RFC 9204 section 2.2.2.3
    leaves the timing to the decoder. B.4's cancellation therefore reads `48 01`, the RFC's `48`
    followed by the duplicate's increment. Stream Cancellations are omitted at capacity 0 (section
    2.2.2.2 allows it; decision 8 has the decoder stream carry nothing).
  - The encoder uses the static table and literals only: Indexed Field Line for a whole static line,
    Literal with static Name Reference for a static name (the first entry with that name), Literal
    with Literal Name otherwise; a never-indexed line is always a literal with the N bit. On the
    decoder stream it accepts Stream Cancellations and refuses Section Acknowledgments and Insert
    Count Increments with `QPACK_DECODER_STREAM_ERROR`, since it never references the dynamic table.
- Not touched (outside `touches`): the Glossary's QPACK row still says "planned"; the
  `align-and-document` pass can mark it built.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Surl.Qpack decodes and encodes QPACK field sections (RFC 9204), replays Appendix B, and refuses dynamic-table use at the server's capacity 0
