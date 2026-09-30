---
id: BL-150
title: Hand-build GF(2^255-19) field arithmetic and X25519 in Surl.Cryptography.Curve25519
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-149]
touches: [Surl.Cryptography.Curve25519.UnitLibrary, Surl.Cryptography.Curve25519.UnitTests]
requirement: FR-039
created: 2026-09-29
completed:
---
# BL-150 — Hand-build GF(2^255-19) field arithmetic and X25519 in Surl.Cryptography.Curve25519

## Goal

`Surl.Cryptography.Curve25519` computes X25519 (RFC 7748 section 5) in constant time, and
exposes the GF(2^255-19) field arithmetic Ed25519 (BL-151) builds on, so the SSH server can
offer `curve25519-sha256` (BL-167).

## Context

- Decision: BL-148's ADR (what this library holds and that it references nothing).
- Shape, public because other projects call it: a static `X25519` with the scalar
  multiplication (`scalar`, `u-coordinate` -> 32 bytes) and the public-key form (base point 9),
  clamping and decoding as RFC 7748 sections 5 and 5.1 state (the top bit of the u-coordinate
  masked). The field element type offers what Ed25519 point arithmetic needs: add, subtract,
  multiply, square, invert, the `(p-5)/8` power used for square roots, conditional swap and
  select, canonical encode and decode (RFC 8032 section 5.1.3 rejects a non-canonical y, so
  decode reports whether the input was canonical).
- Constant time: no branch or table index depends on a secret (a Montgomery ladder with a
  conditional swap, RFC 7748 section 5); document this on the type.
- The all-zero shared-secret check is the SSH layer's (RFC 8731 section 3), not this
  library's: return the output as computed.
- Pure computation, allocation-light, no I/O. The Curl port's `Field25519.cs` and `X25519.cs`
  may be copied as code; every expected value comes from the RFC (ADR-0003).

## Acceptance criteria

- [ ] Tests pass RFC 7748 section 5.2's two single-step vectors and the iterated vector after 1
      and after 1,000 iterations (the 1,000,000-iteration value is not run in the fast tests),
      and section 6.1's Diffie-Hellman example (both public keys and the shared secret); each
      expected value is copied from the RFC, cited beside it.
- [ ] Field-arithmetic tests show `a * invert(a) == 1` for cited non-trivial values, a
      non-canonical encoding (a value >= p) is reported as non-canonical, and encode after
      decode round-trips canonical inputs.
- [ ] `Surl.Cryptography.Curve25519.UnitLibrary.csproj` references nothing.
- [ ] `dotnet build Surl.Cryptography.Curve25519.UnitLibrary -warnaserror` is clean; the fast
      tests pass; `Measure-CodeQuality.ps1 -Library Surl.Cryptography.Curve25519.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
