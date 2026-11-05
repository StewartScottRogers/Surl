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
completed:
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

- [ ] Both projects exist and are listed in `Surl.slnx` in ordinal order. The production csproj has no
      `PackageReference`. `ProtocolIsolationTests` knows the row and passes.
- [ ] Every example in RFC 9204 Appendix B decodes to the RFC's field lines and table state, and the
      encoder reproduces the RFC's bytes for the representations the ADR has the server use.
- [ ] With the dynamic table capacity the ADR advertises, a field section that references an entry
      outside it fails with `QPACK_DECOMPRESSION_FAILED`, and an encoder-stream instruction that
      overflows the table fails with `QPACK_ENCODER_STREAM_ERROR`. Each has a named test.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library has 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
