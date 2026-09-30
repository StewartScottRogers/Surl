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
completed:
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

- [ ] A fast test completes `curve25519-sha256` with the test-side client, verifies the host-key
      signature, and both sides derive the same keys.
- [ ] Fast tests cover a client public value that is not 32 bytes and a low-order point giving an
      all-zero shared secret (a published low-order u-coordinate, cited), each ending the
      connection with the ADR's `DISCONNECT`.
- [ ] The KEXINIT fixture test of BL-159 now shows `curve25519-sha256` in the ADR's position.
- [ ] `ProtocolIsolationTests` pass; `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is
      clean; the fast tests pass with no socket opened;
      `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
