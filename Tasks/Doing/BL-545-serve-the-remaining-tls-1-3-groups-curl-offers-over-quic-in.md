---
id: BL-545
title: Serve the remaining TLS 1.3 groups curl offers over QUIC in Surl.Tls13
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-544]
touches: [Cryptography/Surl.Tls13.UnitLibrary, Cryptography/Surl.Tls13.UnitTests]
requirement: FR-055
created: 2026-11-04
completed:
---
# BL-545 — Serve the remaining TLS 1.3 groups curl offers over QUIC in Surl.Tls13

## Goal

`Surl.Tls13` completes a handshake through a HelloRetryRequest for every group the pinned
OpenSSL builds list in `supported_groups` that ADR-0082 decision 3 does not already serve:
SecP256r1MLKEM768 (`0x11EB`), SecP384r1MLKEM1024 (`0x11EE`), x448 (`0x001E`), curveSM2
(`0x0029`), ffdhe2048 (`0x0100`) and ffdhe3072 (`0x0101`). A curl told
`--curves <that group>` then reaches HTTP/3.

## Context

- Decision: ADR-0082 decision 3. These groups come after the five served ones in the server's
  order. A curl offers them only when its `--curves` option names nothing else.
- Each piece is hand-built where the BCL cannot be relied on for all three platforms:
  - ML-KEM-1024 extends BL-544's `Surl.Cryptography.MlKem`. If it needs that project, add it to
    `touches` as the board rules say.
  - x448 (RFC 7748) and SM2 key exchange (GB/T 32918, RFC 8998 for TLS 1.3) are new primitives.
    If they need their own `Cryptography/` libraries, a new ADR-0002 row needs an ADR.
  - The NIST curves inside the hybrids use the BCL's `ECDiffieHellman`.
  - ffdhe (RFC 7919) is modular exponentiation with `System.Numerics.BigInteger`.
- Measure first: run each pinned `HTTP3` build with `--curves <group> --http3-only` through
  `Record-CurlExchange.ps1 -QuicInitial` and record which share it sends. A group that a build
  refuses to offer alone is recorded and needs no server code.

## Acceptance criteria

- [ ] Notes record, for each listed group, what each pinned build sends with `--curves <group>`.
- [ ] For each group that any pinned build offers, a named test shows that a ClientHello offering
      only that group gets a HelloRetryRequest naming it. The handshake that follows then
      completes against the test-side TLS 1.3 client.
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the touched libraries keep
      100% line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Filed by BL-372 (ADR-0082 decision 3).
- 2026-11-06: Backlog -> Doing.
