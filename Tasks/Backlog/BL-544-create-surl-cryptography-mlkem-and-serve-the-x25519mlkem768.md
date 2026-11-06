---
id: BL-544
title: Create Surl.Cryptography.MlKem and serve the X25519MLKEM768 group in Surl.Tls13
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-376]
touches: [Surl.slnx, Cryptography/Surl.Cryptography.MlKem.UnitLibrary, Cryptography/Surl.Cryptography.MlKem.UnitTests, Cryptography/Surl.Tls13.UnitLibrary, Cryptography/Surl.Tls13.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: FR-055
created: 2026-11-04
completed:
---
# BL-544 — Create Surl.Cryptography.MlKem and serve the X25519MLKEM768 group in Surl.Tls13

## Goal

`Surl.Cryptography.MlKem.UnitLibrary` implements ML-KEM-768 (FIPS 203) by hand. `Surl.Tls13`'s
server handshake then chooses X25519MLKEM768 (`0x11EC`) first, as ADR-0082 decision 3 orders it,
whenever the client sends a key share for it. Every measured upstream curl build sends that share.

## Context

- Decision: ADR-0082 decisions 2 and 3. The ADR-0002 row is already recorded:
  `Surl.Cryptography.MlKem.UnitLibrary` references nothing, and `Surl.Tls13.UnitLibrary` may
  reference it.
- Why it is hand-built: the BCL's `MLKem` needs OpenSSL 3.5 on Linux or a Windows Insider build,
  and does not exist on Apple platforms ("Cross-platform cryptography", checked 2026-11-04).
- To create the project, copy BL-292 (commit `5008a375`): the csproj pair, `CLAUDE.md`, ordinal
  `Surl.slnx` lines in the `/Cryptography/` folder, and the `ProtocolIsolationTests` row.
- FIPS 203: ML-KEM.KeyGen_internal, Encaps_internal and Decaps_internal, with parameter set
  ML-KEM-768. The server only encapsulates: it takes the client's 1184-byte encapsulation key
  and returns a 1088-byte ciphertext and a 32-byte shared secret. Decapsulation and key
  generation are built too, so the tests can run a full round trip. SHA3-256, SHA3-512, SHAKE128
  and SHAKE256 are not in the BCL on macOS, so Keccak is hand-built inside this library.
- X25519MLKEM768 follows draft-ietf-tls-ecdhe-mlkem. The client's share is the ML-KEM-768
  encapsulation key (1184 bytes) followed by its x25519 key (32 bytes), 1216 bytes in all, as
  ADR-0082 measured. The server's share is the ciphertext (1088 bytes) followed by its x25519 key.
  The shared secret is ML-KEM's secret followed by x25519's.
- Fixture: BL-363's recorded ClientHello from each pinned `HTTP3` build. Each one carries an
  X25519MLKEM768 share.

## Acceptance criteria

- [ ] `Surl.Cryptography.MlKem.UnitLibrary` and `.UnitTests` exist, are listed in `Surl.slnx`
      under `/Cryptography/`, and `ProtocolIsolationTests` knows the ADR-0082 row.
- [ ] Tests reproduce known-answer vectors for ML-KEM-768 key generation, encapsulation and
      decapsulation. The vectors come from NIST's ACVP or FIPS 203 example values, and a test
      comment names their source and URL.
- [ ] With BL-363's recorded ClientHello, `Surl.Tls13` selects X25519MLKEM768 and answers with a
      1120-byte key share. The test-side TLS 1.3 client from BL-376 completes the handshake.
- [ ] A ClientHello with only an x25519 share still gets x25519, with no HelloRetryRequest.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and both libraries keep
      100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Filed by BL-372 (ADR-0082 decision 3).
