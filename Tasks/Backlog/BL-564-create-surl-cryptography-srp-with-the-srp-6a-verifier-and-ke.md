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
completed:
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

- [ ] Named tests reproduce every value of RFC 5054 Appendix B (`k`, `x`, `v`, `a`, `b`, `A`, `B`,
      `u`, premaster secret) byte for byte.
- [ ] Named tests check each Appendix A group the ADR serves against the RFC's `N` and `g`, reject a
      client `A` with `A % N == 0`, and read a verifier file in the ADR's format (and refuse a
      malformed one with the error the ADR names).
- [ ] `dotnet build Cryptography/Surl.Cryptography.Srp.UnitLibrary -warnaserror` is clean, the fast
      tests are green, and the library has 100% line and branch coverage, complexity of at most 10
      per method and CRAP of at most 30.

## Notes

## Log

- 2026-11-04: Created.
