---
id: BL-365
title: Create Surl.Hpack and implement HPACK header compression
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-364]
touches: [Surl.slnx, Surl.Hpack.UnitLibrary, Surl.Hpack.UnitTests, Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-04
completed:
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
  `Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs`.
- RFC 7541 covers integer representation (5.1), string literals (5.2), the static table (Appendix A),
  the dynamic table and its size updates (4, 6.3), and the Huffman code (Appendix B).
- A decoder bounds what it holds by the header-list limit the ADR maps from `--max-message` (ADR-0006
  section 1). It accepts no table-size update above the value the server advertised.

## Acceptance criteria

- [ ] Both projects exist and are listed in `Surl.slnx` in ordinal order. The production csproj has no
      `PackageReference`. `ProtocolIsolationTests` knows the row and passes.
- [ ] Tests decode every request example in RFC 7541 Appendix C (C.2 to C.6, with and without Huffman)
      to the RFC's header lists and dynamic-table states, and encode them back to the RFC's bytes.
- [ ] Each malformed input fails with its own named error and never throws an unhandled exception.
      Each case has a test: integer overflow, an index outside both tables, a truncated string, a
      Huffman string with the EOS symbol or padding longer than 7 bits, a size update above the
      limit, and a header list over the bound.
- [ ] `dotnet build -warnaserror` is clean (complexity at most 10 per method), the fast tests are
      green, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for the library.

## Notes

## Log

- 2026-11-04: Created.
