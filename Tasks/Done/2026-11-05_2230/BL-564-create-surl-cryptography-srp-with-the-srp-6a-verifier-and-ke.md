---
id: BL-564
title: Create Surl.Cryptography.Srp with the SRP-6a verifier and key exchange
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-563]
touches: [Surl.slnx, Cryptography/Surl.Cryptography.Srp.UnitLibrary, Cryptography/Surl.Cryptography.Srp.UnitTests, Foundation/Surl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-05
---
# BL-564 — Create Surl.Cryptography.Srp with the SRP-6a verifier and key exchange

## Goal

`Surl.Cryptography.Srp.UnitLibrary` exists with its `.UnitTests` twin and computes the server side
of SRP-6a as RFC 5054 defines it: the verifier from salt, user and password, the multiplier `k`,
the server's public value `B`, the scrambler `u`, and the premaster secret from the client's `A`,
for each of RFC 5054 Appendix A's groups the ADR serves, and reads the verifier file format the ADR
names.

## Context

- Decision: BL-563's ADR, decisions 1 and 4. The library name and folder are the ADR's.
- Specification: RFC 5054 sections 2.4 to 2.6 (`x = SHA1(s | SHA1(I | ":" | P))`, `v = g^x % N`,
  `k = SHA1(N | PAD(g))`, `B = k*v + g^b % N`, `u = SHA1(PAD(A) | PAD(B))`,
  `S = (A * v^u) ^ b % N`), section 2.5.4 (reject `A % N == 0`), Appendix A (groups) and Appendix B
  (test vectors: `I = "alice"`, `P = "password123"`, the 1024-bit group).
- BCL only: `System.Numerics.BigInteger`, `SHA1`, `RandomNumberGenerator` (injected, so tests fix
  `b`). New project steps: add both projects to `Surl.slnx` under `/Cryptography/` in ordinal
  order, give the library its `CLAUDE.md`, and update `Surl.Protocol.Abstractions.UnitTests`'
  project-reference rules if they list the hand-built primitives.
- Upstream test cases that wait on this (tag `curl-8_21_0`), `httptls+srp`: 320, 321, 322, 324.
  BL-568 runs them.

## Acceptance criteria

- [x] Named tests reproduce every value of RFC 5054 Appendix B (`k`, `x`, `v`, `a`, `b`, `A`, `B`,
      `u`, premaster secret) byte for byte.
- [x] Named tests check each Appendix A group the ADR serves against the RFC's `N` and `g`, reject a
      client `A` with `A % N == 0`, and read a verifier file in the ADR's format (and refuse a
      malformed one with the error the ADR names).
- [x] `dotnet build Cryptography/Surl.Cryptography.Srp.UnitLibrary -warnaserror` is clean, the fast
      tests are green, and the library has 100% line and branch coverage, complexity of at most 10
      per method and CRAP of at most 30.

## Notes

- 2026-11-05 (lane 7): built `Surl.Cryptography.Srp.UnitLibrary` (references nothing) and its
  `.UnitTests` twin, added to `Surl.slnx` under `/Cryptography/` and to
  `ProtocolIsolationTests`' horizontal-library table with an empty row (ADR-0122 decision 1).
  Types: `SrpGroup` (all seven Appendix A groups, `IsRfc5054Group`), `Srp6a` (formulas),
  `SrpServerKeyExchange` (one handshake's server half, `b` from an injected
  `RandomNumberGenerator`), `GnuTlsSrpBase64`, `SrpGroupFile`, `SrpVerifierFile`,
  `SrpVerifierEntry`, `SrpFileFormatException`.
- Appendix A primes and Appendix B vectors were copied from the RFC text
  (rfc-editor.org/rfc/rfc5054.txt); the group tests pin each prime by the SHA-256 of the RFC's
  hex, computed from that text, rather than repeating 5000 hex digits.
- Choices (sensible defaults): `B = (k*v + g^b) % N` (Appendix B's `B` confirms the reduction);
  `B` and the premaster secret come out with no leading zero byte, as OpenSSL's `BN_bn2bin`
  gives them (BL-565/566 should confirm against curl's handshake); `b` is 32 bytes, RFC 5054
  section 2.5.3's minimum of 256 bits; `A` longer than `N` throws `ArgumentOutOfRangeException`
  because the caller answers it with `decode_error` before asking, while `A % N = 0` is a
  `false` from `TryComputePremasterSecret` (the `illegal_parameter` case).
- The readers take text, not a path, so the library opens no file; the host reads the file and
  maps `SrpFileFormatException` (with `LineNumber`) to `CouldNotReadFile` (37). Blank lines are
  skipped and a CR before LF is dropped; a repeated group index is malformed; a user given twice
  is left to the host, which ADR-0122 makes `FailedInit` (2).
- GnuTLS SRP base64: a leading group of 1, 2 or 3 digits stands for 1, 1 or 2 bytes, and one
  holding more bits than that is refused. Checked against GnuTLS's own `tpasswd.conf` line for
  the 1024-bit group, which decodes to RFC 5054's prime exactly. Upstream's
  `srp-verifier-db` itself is not in the repository yet (BL-568 imports it); that `jsmith`/`abc`
  logs in against it is BL-568's check of this reader.
- Coverage: 74 tests; the library measures 100% line and 100% branch with the MSTest coverage
  collector; complexity is enforced by the build (clean).

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Cryptography.Srp computes RFC 5054's SRP-6a server side (Appendix B byte for byte, all seven Appendix A groups) and reads GnuTLS tpasswd and tpasswd.conf files
