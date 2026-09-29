---
id: BL-112
title: Hand-build SHA-512/256 in Surl.Cryptography
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Surl.Cryptography.UnitLibrary, Surl.Cryptography.UnitTests]
requirement: FR-014
created: 2026-09-29
completed:
---
# BL-112 — Hand-build SHA-512/256 in Surl.Cryptography

## Goal

`Surl.Cryptography` computes SHA-512/256 (FIPS 180-4 section 5.3.6.2 and 6.7), which the
BCL does not offer, so the Digest verifier (BL-113) can answer `algorithm=SHA-512-256`.

## Context

RFC 7616 names `SHA-512-256` as a Digest algorithm; .NET's `SHA512` cannot take the
SHA-512/256 initial hash values, so it is built by hand (root `CLAUDE.md`, "Decisions":
hand-built pieces live in their own `Surl.<Area>.UnitLibrary`). Measured 2026-09-29 while
planning: the pinned Windows reference build (curl 8.21.0, SSPI) answers a SHA-512-256
challenge with exit 94, so this algorithm serves the Linux and macOS reference builds, whose
Digest is curl's own code.

- `Surl.Cryptography.UnitLibrary` is empty (only `CLAUDE.md` and a csproj that references
  nothing - ADR-0002 keeps it that way); `Surl.Cryptography.UnitTests` likewise.
- Shape: a static one-shot `Sha512Slash256.HashData(ReadOnlySpan<byte>)` returning 32 bytes
  (or the name the `CLAUDE.md` of the library prefers), plus an incremental form only if
  BL-113 needs one (Digest hashes short strings, so one-shot suffices).
- Pure computation: no I/O, no time, allocation-light; `BinaryPrimitives` for big-endian
  words.

## Acceptance criteria

- [ ] Tests pass the FIPS 180-4 example vectors for SHA-512/256 (NIST CSRC "Examples with
      Intermediate Values": `"abc"` and the two-block 896-bit message), and vectors taken from
      NIST CAVP's SHAVS response files `SHA512_256ShortMsg.rsp` and `SHA512_256LongMsg.rsp`
      covering the empty message and lengths either side of the 111/112- and 128-byte padding
      boundaries; every expected value is copied from a published source named beside it in
      the test, never computed by the code under test.
- [ ] A test proves the one-shot result equals the incremental result for split inputs, if
      an incremental form exists.
- [ ] `Surl.Cryptography.UnitLibrary.csproj` still references nothing.
- [ ] `dotnet build Surl.Cryptography.UnitLibrary -warnaserror` is clean; the fast tests
      pass; 100% line and branch coverage; no method exceeds complexity 10.

## Notes

## Log

- 2026-09-29: Created.
