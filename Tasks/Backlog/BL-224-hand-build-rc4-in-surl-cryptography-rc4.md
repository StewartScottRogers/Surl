---
id: BL-224
title: Hand-build RC4 in Surl.Cryptography.Rc4
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Cryptography.Rc4.UnitLibrary, Surl.Cryptography.Rc4.UnitTests, Surl.slnx, Surl.Protocol.Abstractions.UnitTests]
requirement: FR-039
created: 2026-09-29
completed:
---
# BL-224 — Hand-build RC4 in Surl.Cryptography.Rc4

## Goal

`Surl.Cryptography.Rc4.UnitLibrary` exists beside its `.UnitTests` twin, is listed in
`Surl.slnx` and guarded by `ProtocolIsolationTests`, and produces the RC4 keystream (XORed into
a span) both from the first byte and after discarding the first N bytes, so BL-221 can build
the `arcfour` and `arcfour128` SSH ciphers from it.

## Context

- Decision: ADR-0051 (BL-154) - it adds `Surl.Cryptography.Rc4.UnitLibrary` to ADR-0002
  decision 3's table of horizontal libraries (as ADR-0048 did for the four SSH primitive
  libraries), referencing nothing. The BCL has no RC4, so it is hand-built (root `CLAUDE.md`,
  "Decisions").
- Creating the projects: follow `.claude/skills/new-project/SKILL.md` and copy what BL-149
  (`Tasks/Done/BL-149-create-the-curve25519-ed25519-chacha20-and-poly1305-primitiv.md`) did for
  the four SSH primitive libraries:
  - csproj shapes from `Surl.Cryptography.ChaCha20.UnitLibrary` and
    `Surl.Cryptography.ChaCha20.UnitTests` (the library only `InternalsVisibleTo` its tests; the
    test project has the `MSTest` package with no `Version`, the global `Using`, the one
    `ProjectReference`);
  - `Surl.slnx`: the two `<Project>` lines in ordinal order in the flat run, library then
    `.UnitTests`, after `Surl.Cryptography.Poly1305.UnitTests` and before
    `Surl.Cryptography.UnitLibrary`, no solution folder;
  - `Surl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs`: a `HorizontalLibraries`
    row `[Rc4] = []` and the `ForbiddenProtocolReferences_AllowedLibrary_IsNotForbidden` data
    row for it;
  - `Surl.Cryptography.Rc4.UnitLibrary/CLAUDE.md` in the shape of
    `Surl.Cryptography.ChaCha20.UnitLibrary/CLAUDE.md`: what it holds, that it references
    nothing, bytes in and bytes out (no socket, file or clock), vectors cited beside each test,
    copying from the Curl port allowed as code only (ADR-0003).
- Specifications: RFC 6229 ("Test Vectors for the Stream Cipher RC4": keystream at offsets 0 to
  4096 for 40- to 256-bit keys); RFC 4253 section 6.3 (`arcfour`, 128-bit key, no discard);
  RFC 4345 section 4 (`arcfour128` and `arcfour256` discard the first 1536 bytes of keystream).
- Shape: an `Rc4` type constructed from a key of 1 to 256 bytes (argument exception otherwise)
  and a count of keystream bytes to discard, with a method that XORs the next keystream bytes
  from an input span into an output span and keeps its state across calls (an SSH connection
  runs one keystream across every packet). Name the members for what they do.

## Acceptance criteria

- [ ] `Surl.Cryptography.Rc4.UnitLibrary` and `Surl.Cryptography.Rc4.UnitTests` exist at the
      repository root; the production csproj has no `PackageReference` and no
      `ProjectReference`; `Surl.slnx` lists both in the position given in Context.
- [ ] `Surl.Cryptography.Rc4.UnitLibrary/CLAUDE.md` exists with the content listed in Context.
- [ ] `ProtocolIsolationTests` has the `Rc4` row and passes, including
      `EveryHorizontalLibrary_ReferencesOnlyItsRow`.
- [ ] Tests reproduce RFC 6229's keystream for the 40-, 128- and 256-bit keys at offsets 0, 16,
      1536 and 4096, each expected value copied from the RFC and cited beside it.
- [ ] A test shows that an `Rc4` discarding 1536 bytes produces, from its first byte, RFC 6229's
      offset-1536 row for the 128-bit and the 256-bit key (the RFC 4345 form).
- [ ] A test shows that keystream taken across several calls equals the same keystream taken in
      one call; tests show the refusal of an empty key and of a 257-byte key.
- [ ] `dotnet build Surl.Cryptography.Rc4.UnitLibrary -warnaserror` is clean; `dotnet build`
      and `dotnet test --filter "TestCategory!=Integration"` are green;
      `Measure-CodeQuality.ps1 -Library Surl.Cryptography.Rc4.UnitLibrary` reports 100% line
      and 100% branch coverage, no method above cyclomatic complexity 10, and no failing member.

## Notes

## Log

- 2026-09-29: Created.
