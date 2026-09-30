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
completed: 2026-09-29
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

- [x] Tests pass RFC 8032 section 7.1's TEST 1, TEST 2, TEST 3, TEST 1024 and TEST SHA(abc):
      the public key from the secret key, the signature, and verification; each expected value
      copied from the RFC and cited beside it.
- [x] Tests show `Verify` returns false for a flipped message bit, a flipped signature bit, an S
      equal to L, and a public key whose encoding does not decode.
- [x] `Surl.Cryptography.Ed25519.UnitLibrary.csproj` references only
      `Surl.Cryptography.Curve25519.UnitLibrary`.
- [x] `dotnet build Surl.Cryptography.Ed25519.UnitLibrary -warnaserror` is clean; the fast
      tests pass; `Measure-CodeQuality.ps1 -Library Surl.Cryptography.Ed25519.UnitLibrary`
      reports 100% line and branch coverage and no failing member.

## Notes

- Code copied from the Curl port's `Ed25519.cs`, `Edwards25519.cs` and `Scalar25519.cs`
  (and their tests) as code only; every expected value in the tests is RFC 8032's
  (section 7.1 vectors, section 5.1 for L and the base point) or computed with
  `BigInteger` (ADR-0003).
- Adapted to Surl's `Field25519`, which has no general `PowerByPublicExponent`: the
  square-root exponent uses `PowerPMinus5Over8`, and sqrt(-1) = 2^((p-1)/4) is computed as
  2 * (2^((p-5)/8))^2. `TryDecode` takes y's canonicity from `Field25519.Decode`'s return
  instead of re-encoding.
- Default taken: the API returns arrays (`byte[] ComputePublicKey(seed)`,
  `byte[] Sign(seed, message)`) as the task's shape states, and throws `ArgumentException`
  for a wrong-length seed, key or signature. The port's `GeneratePrivateKey` was left out:
  the task does not name it, and a caller can draw a seed from `RandomNumberGenerator`.
- Measured: 55 tests pass; `Measure-CodeQuality.ps1 -Library Surl.Cryptography.Ed25519.UnitLibrary`
  reports 100% line, 100% branch, 36 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. Ed25519 derives public keys, signs and verifies per RFC 8032 5.1, passing section 7.1's vectors
