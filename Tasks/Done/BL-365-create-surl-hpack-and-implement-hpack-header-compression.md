---
id: BL-365
title: Create Surl.Hpack and implement HPACK header compression
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-364]
touches: [Surl.slnx, Protocols/Http/Surl.Hpack.UnitLibrary, Protocols/Http/Surl.Hpack.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-365 — Create Surl.Hpack and implement HPACK header compression

## Goal

`Surl.Hpack.UnitLibrary` and `Surl.Hpack.UnitTests` exist and are listed in `Surl.slnx`, and the
library encodes and decodes HPACK header blocks (RFC 7541) within the bounds that BL-364's ADR sets.
That includes the Huffman code, which BL-382's QPACK reuses.

## Context

- Decision: BL-364's ADR, decision 4. `Surl.Hpack` is the planning name. If the ADR names the library
  differently, use its name everywhere below. If it places HPACK inside `Surl.Protocol.Http`, do the
  same work there with no new project, and leave `Surl.slnx` and `ProtocolIsolationTests` untouched.
- To create the projects, copy BL-292's change for `Surl.HttpMessage` (commit `5008a375`): a production
  csproj with the references the ADR allows and `InternalsVisibleTo` its tests, a test csproj that uses
  MSTest through central package management with no `Version`, a `CLAUDE.md`, two `Surl.slnx` lines in
  ordinal order with the tests directly after the library, and the library's row in
  `Foundation/Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs`.
- RFC 7541 covers integer representation (5.1), string literals (5.2), the static table (Appendix A),
  the dynamic table and its size updates (4, 6.3), and the Huffman code (Appendix B).
- A decoder bounds what it holds by the header-list limit the ADR maps from `--max-message` (ADR-0006
  section 1). It accepts no table-size update above the value the server advertised.

## Acceptance criteria

- [x] Both projects exist and are listed in `Surl.slnx` in ordinal order. The production csproj has no
      `PackageReference`. `ProtocolIsolationTests` knows the row and passes.
- [x] Tests decode every request example in RFC 7541 Appendix C (C.2 to C.6, with and without Huffman)
      to the RFC's header lists and dynamic-table states, and encode them back to the RFC's bytes.
- [x] Each malformed input fails with its own named error and never throws an unhandled exception.
      Each case has a test: integer overflow, an index outside both tables, a truncated string, a
      Huffman string with the EOS symbol or padding longer than 7 bits, a size update above the
      limit, and a header list over the bound.
- [x] `dotnet build -warnaserror` is clean (complexity at most 10 per method), the fast tests are
      green, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for the library.

## Notes

- ADR-0081 decision 4 names the library `Surl.Hpack.UnitLibrary`, in `Protocols/Http/`, referencing
  nothing. `touches` now gives the two projects' real paths under `Protocols/Http/` (the same
  projects, so no other task's `touches` changes).
- The decoder, the encoder, the tables and the Huffman code started as a copy of the Curl port's
  `Curl.Http2` HPACK code. Copying code is allowed (ADR-0003). Every expected byte in the tests comes
  from RFC 7541 Appendix C, never from the port.
- The decoder takes the advertised `SETTINGS_HEADER_TABLE_SIZE` and `SETTINGS_MAX_HEADER_LIST_SIZE`
  (`--max-message`, ADR-0081 decision 5). `Decode` never throws on input: it returns an
  `HpackDecodeResult` whose `Error` names one of ten `HpackDecodingError`s. Truncation splits into
  `TruncatedInteger` and `TruncatedString`, and bad Huffman padding splits into
  `HuffmanPaddingLongerThanSevenBits` and `HuffmanPaddingNotAllOnes`. An update after a field is
  `TableSizeUpdateAfterField`.
- Default choice: the encoder follows the representation choices RFC 7541 Appendix C makes. The
  field's `HpackFieldIndexing` picks the literal kind, and an exact static or dynamic match is sent as
  an index unless the field is never-indexed. That way Appendix C encodes back to its own bytes.
  `HpackStringCoding` (`WhenShorter` by default, or `Huffman` or `Raw`) picks the string coding.
  Curl's choices (nghttp2's indexing heuristics) do not matter here, because the server only has to
  produce blocks that curl can decode.
- `HpackHuffman` is public for QPACK (BL-382). The integer and string primitives stay internal until
  QPACK needs them.
- Tests cover all of Appendix C: C.1 (integers), C.2 (the four literal kinds), and C.3 to C.6
  (requests and responses, raw and Huffman), decoded to the RFC's lists and tables and encoded back
  to its bytes. `Measure-CodeQuality.ps1 -Library Surl.Hpack.UnitLibrary` reports 100% line and
  100% branch coverage, with 0 failing members. Of 75 tests, 75 pass.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Surl.Hpack encodes and decodes HPACK (RFC 7541) with every Appendix C example both ways, named errors for malformed blocks, 100% coverage
