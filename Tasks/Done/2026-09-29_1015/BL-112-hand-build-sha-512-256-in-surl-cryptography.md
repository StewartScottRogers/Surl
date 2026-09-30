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
completed: 2026-09-29
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

- [x] Tests pass the FIPS 180-4 example vectors for SHA-512/256 (NIST CSRC "Examples with
      Intermediate Values": `"abc"` and the two-block 896-bit message), and vectors taken from
      NIST CAVP's SHAVS response files `SHA512_256ShortMsg.rsp` and `SHA512_256LongMsg.rsp`
      covering the empty message and lengths either side of the 111/112- and 128-byte padding
      boundaries; every expected value is copied from a published source named beside it in
      the test, never computed by the code under test.
- [x] A test proves the one-shot result equals the incremental result for split inputs, if
      an incremental form exists.
- [x] `Surl.Cryptography.UnitLibrary.csproj` still references nothing.
- [x] `dotnet build Surl.Cryptography.UnitLibrary -warnaserror` is clean; the fast tests
      pass; 100% line and branch coverage; no method exceeds complexity 10.

## Notes

- Delivered directly rather than through every `/feature` agent: the task is one
  self-contained primitive with a fixed specification, so the plan is the task itself.
- Shape: `public static class Sha512Slash256` with `HashData(ReadOnlySpan<byte>)` returning
  32 bytes (`HashLength`). Public, because BL-113's verifier in `Surl.Authentication` calls
  it. No incremental form: Digest hashes short strings, so criterion 2 does not apply.
- Compression code adapted from the Curl port's `Sha512Slash256` (code is not a verdict,
  ADR-0003); padding rewritten to hash whole blocks straight from the span and pad the tail
  in a stack buffer, so the only allocation is the returned hash.
- Vectors: `"abc"` and the 896-bit message from NIST's SHA512_256 example PDF; CAVP byte
  vectors (CAVS 21.1) for Len 0, 880, 888, 896, 904, 1016, 1024 from `SHA512_256ShortMsg.rsp`
  and Len 1816 from `SHA512_256LongMsg.rsp`, read from the copy pyca/cryptography vendors
  (`vectors/cryptography_vectors/hashes/SHA2/`).
- Coverage of `Surl.Cryptography.UnitLibrary`: 100% line, 100% branch; build clean with
  warnings as errors, so CA1502 holds complexity at 10 or below.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Surl.Cryptography computes SHA-512/256, pinned to NIST example and CAVP vectors
