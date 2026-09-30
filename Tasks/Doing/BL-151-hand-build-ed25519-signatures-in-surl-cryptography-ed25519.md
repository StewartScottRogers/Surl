---
id: BL-151
title: Hand-build Ed25519 signatures in Surl.Cryptography.Ed25519
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-150]
touches: [Surl.Cryptography.Ed25519.UnitLibrary, Surl.Cryptography.Ed25519.UnitTests]
requirement: FR-039
created: 2026-09-29
completed:
---
# BL-151 — Hand-build Ed25519 signatures in Surl.Cryptography.Ed25519

## Goal

`Surl.Cryptography.Ed25519` derives an Ed25519 public key from a 32-byte seed, signs and
verifies (RFC 8032 section 5.1), so the SSH server can serve an `ssh-ed25519` host key and
check `ssh-ed25519` user keys (BL-168).

## Context

- Decision: BL-148's ADR (this library references `Surl.Cryptography.Curve25519` for the field
  arithmetic, and nothing else). SHA-512 comes from the BCL (`SHA512.HashData`).
- Shape: a static `Ed25519` with `ComputePublicKey(seed)`, `Sign(seed, message)` (64 bytes) and
  `Verify(publicKey, message, signature)` (bool); Edwards-curve point arithmetic (extended
  coordinates) and scalar arithmetic mod L live here, internal.
- Verification refuses what RFC 8032 section 5.1.7 refuses: an S not below L, a point
  encoding that does not decode (section 5.1.3), a non-canonical y. Signing is constant-time in
  the secret (no secret-dependent branch or index).
- The Curl port's `Ed25519.cs`, `Edwards25519.cs` and `Scalar25519.cs` may be copied as code;
  every expected value comes from RFC 8032 (ADR-0003).

## Acceptance criteria

- [ ] Tests pass RFC 8032 section 7.1's TEST 1, TEST 2, TEST 3, TEST 1024 and TEST SHA(abc):
      the public key from the secret key, the signature, and verification; each expected value
      copied from the RFC and cited beside it.
- [ ] Tests show `Verify` returns false for a flipped message bit, a flipped signature bit, an S
      equal to L, and a public key whose encoding does not decode.
- [ ] `Surl.Cryptography.Ed25519.UnitLibrary.csproj` references only
      `Surl.Cryptography.Curve25519.UnitLibrary`.
- [ ] `dotnet build Surl.Cryptography.Ed25519.UnitLibrary -warnaserror` is clean; the fast
      tests pass; `Measure-CodeQuality.ps1 -Library Surl.Cryptography.Ed25519.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
