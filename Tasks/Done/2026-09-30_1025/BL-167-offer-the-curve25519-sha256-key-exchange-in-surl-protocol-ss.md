---
id: BL-167
title: Offer the curve25519-sha256 key exchange in Surl.Protocol.Ssh
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-161, BL-150]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests]
requirement: FR-039
created: 2026-09-29
completed: 2026-09-30
---
# BL-167 — Offer the curve25519-sha256 key exchange in Surl.Protocol.Ssh

## Goal

`SshProtocolServer` offers and completes `curve25519-sha256` (and the
`curve25519-sha256@libssh.org` name, if BL-154's ADR lists it) over the hand-built X25519 in
`Surl.Cryptography.Curve25519`, in the ADR's preference position.

## Context

- Decision: BL-154's ADR (the kex list and order); BL-148's ADR (the library).
- Specification: RFC 8731 (the ECDH exchange with X25519, the shared secret encoded as an
  `mpint`, and section 3's rule that an all-zero shared secret aborts the exchange); RFC 7748
  for X25519; the exchange hash and key derivation are BL-160's.
- Add the `ProjectReference` to `Surl.Cryptography.Curve25519.UnitLibrary`.
- Tested as BL-160 tests its key exchanges: the test-side client uses the same library to
  complete the exchange; randomness is injected.

## Acceptance criteria

- [x] A fast test completes `curve25519-sha256` with the test-side client, verifies the host-key
      signature, and both sides derive the same keys.
- [x] Fast tests cover a client public value that is not 32 bytes and a low-order point giving an
      all-zero shared secret (a published low-order u-coordinate, cited), each ending the
      connection with the ADR's `DISCONNECT`.
- [x] The KEXINIT fixture test of BL-159 now shows `curve25519-sha256` in the ADR's position.
- [x] `ProtocolIsolationTests` pass; `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is
      clean; the fast tests pass with no socket opened;
      `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

- Built as `SshCurve25519KeyExchange`, registered in `SshKeyExchangeMethod` under both `curve25519-sha256` and `curve25519-sha256@libssh.org` (ADR-0051 decision 2 lists both, first and second). The server private key is 32 bytes from the injected `ISshRandomSource`; K is the X25519 output read as an unsigned big-endian integer (RFC 8731 section 3.1), hashed with SHA-256. Private key and secret are zeroed after use; the all-zero check is constant-time.
- Refusals: a `Q_C` that is not 32 bytes, or a low-order one giving an all-zero secret, is `DISCONNECT` 2 "Protocol error" - ADR-0051 decision 9 already answers an invalid client public value (a NIST point off its curve, e outside 1 < e < p - 1) that way, so no new ADR was needed. Low-order u-coordinates tested: 0, 1, both order-8 points and p, from Bernstein's "Which Curve25519 public keys are unsafe?" (https://cr.yp.to/ecdh.html).
- With curve25519 built, every default kex is built, so the "Key exchange not implemented" tests now use `OfferWithAnUnbuiltKeyExchange` (RsaOffer with `diffie-hellman-group14-sha1`, BL-221's, put first) to keep that branch covered. `WronglyGuessedKeyExchangePacket_IsDiscarded` now expects `DISCONNECT` 2: the second message 30 is read as the real curve25519 init and its 2-byte key refused.
- The KEXINIT fixture test (`ExpectedServerKexInitPacket`) already listed `curve25519-sha256` first, as ADR-0051 orders it; unchanged and passing.
- Gates: build -warnaserror clean; fast tests green (Surl.Protocol.Ssh.UnitTests 753 passed; ProtocolIsolationTests in Abstractions.UnitTests passed); Measure-CodeQuality: Surl.Protocol.Ssh.UnitLibrary 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. SshProtocolServer completes curve25519-sha256 and curve25519-sha256@libssh.org over the hand-built X25519, refusing short and low-order keys with DISCONNECT 2
