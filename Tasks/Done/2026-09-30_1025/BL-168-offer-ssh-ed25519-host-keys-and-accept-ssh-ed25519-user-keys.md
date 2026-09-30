---
id: BL-168
title: Offer ssh-ed25519 host keys and accept ssh-ed25519 user keys in Surl.Protocol.Ssh
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-162, BL-151]
touches: [Surl.Protocol.Ssh.UnitLibrary, Surl.Protocol.Ssh.UnitTests, Documentation/Planning/Decisions/ADR-0058-the-ssh-key-exchange-and-host-key-reading-choices-adr-0051-left-open.md]
requirement: FR-039
created: 2026-09-29
completed: 2026-09-30
---
# BL-168 — Offer ssh-ed25519 host keys and accept ssh-ed25519 user keys in Surl.Protocol.Ssh

## Goal

`SshProtocolServer` serves an `ssh-ed25519` host key (signing the exchange hash with the
hand-built Ed25519) and verifies `ssh-ed25519` user-key signatures in `publickey`
authentication, and the host-key reader accepts Ed25519 keys in the formats BL-154's ADR
names.

## Context

- Decision: BL-154's ADR (host-key list and order, host-key file formats); BL-148's ADR (the
  library). Specification: RFC 8709 (the `ssh-ed25519` key and signature encodings), RFC 8032
  (Ed25519), OpenSSH `PROTOCOL.key` for the `openssh-key-v1` private-key format if the ADR
  reads it.
- Add the `ProjectReference` to `Surl.Cryptography.Ed25519.UnitLibrary`.
- Host-key signing is BL-160's path; user-key verification is BL-162's path; this task adds the
  Ed25519 algorithm to both.

## Acceptance criteria

- [x] A fast test completes a key exchange with an `ssh-ed25519` host key and the test-side
      client verifies the signature with `Surl.Cryptography.Ed25519`.
- [x] Fast tests accept a valid `ssh-ed25519` user-key signature for an authorized key, and
      refuse a flipped signature and a key blob of the wrong length.
- [x] The host-key reader accepts an Ed25519 key in each format the ADR names, and refuses the
      malformed cases the ADR lists.
- [x] `ProtocolIsolationTests` pass; `dotnet build Surl.Protocol.Ssh.UnitLibrary -warnaserror` is
      clean; the fast tests pass with no socket opened;
      `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary` reports 100% line and
      branch coverage and no failing member.

## Notes

- Delivered in the session rather than through the full `/feature` agent chain: the change is
  one algorithm added to two existing paths in one library, and the ADRs already decide it.
- `SshEd25519HostKey` (new) serves `ssh-ed25519` from a 32-byte seed with
  `Surl.Cryptography.Ed25519`; `SshUserKeySignature` verifies `ssh-ed25519` (a 32-byte key and a
  64-byte signature, else false) and now lists it first in `server-sig-algs`, as ADR-0051
  decision 2.1 orders it. The ECDSA/RSA paths are unchanged.
- Host-key reader: PKCS #8 (plain, version 2 with a public key, and PBES2-encrypted) and
  `openssh-key-v1`. The malformed cases refused as "not a private key surl can read" are recorded
  in ADR-0058 decision 6 (amended here, decided by Claude under Stewart's delegation), which asked
  BL-168 to amend it. ADR-0058 was added to `touches` for that: no task in Doing names it.
- Test oracle for Ed25519 bytes: RFC 8032 section 7.1 TEST 1 (seed, public key and the
  signature of the empty message), so the host-key signature and user-key verification are
  checked against the RFC and not against the library's own output.
- `SshTestKeys.AllHostKeys()` now holds the Ed25519 key too, so every host-key algorithm is
  offered in the key-exchange tests.
- Verified: `dotnet build` clean (0 warnings); fast tests all green (Ssh 733);
  `Measure-CodeQuality.ps1 -Library Surl.Protocol.Ssh.UnitLibrary`: 100% line, 100% branch,
  0 failing of 522 members, worst CRAP 10; `ProtocolIsolationTests` pass (Abstractions 251).

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. Surl.Protocol.Ssh serves ssh-ed25519 host keys (PKCS #8, encrypted PKCS #8, openssh-key-v1) and verifies ssh-ed25519 publickey logins
