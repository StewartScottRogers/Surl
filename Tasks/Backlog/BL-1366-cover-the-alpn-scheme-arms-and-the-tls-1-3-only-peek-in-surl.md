---
id: BL-1366
title: Cover the ALPN scheme arms and the TLS 1.3-only peek in Surl.Networking
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-17
completed:
---
# BL-1366 — Cover the ALPN scheme arms and the TLS 1.3-only peek in Surl.Networking

## Goal

`Measure-CodeQuality.ps1 -Library Surl.Networking.UnitLibrary` reports 100% line and 100% branch with no failing member.

## Context

- Found by the coverage-auditor's audit of 2026-11-17 (two full runs, same result): 100% line, 97.38% branch, 2 failing
  members, branch-only, in `Foundation/Surl.Networking.UnitLibrary/`:
  - `TlsApplicationProtocols.ForScheme(string)` - `TlsApplicationProtocols.cs:34`, 76.09% branch; line 34 is 35 of 46.
    The compiled string switch over `https`, `wss`, `s3`, `oci`, `mcps`, `az`, `abfss`, `gs`, `grpcs`, `xmpps` and the
    default arm has edges no test reaches - schemes never passed directly, and look-alike strings that share a length
    or hash with a listed scheme.
  - `<get_OffersOnlyTls13>b__14_0(ushort)` - `ClientHelloPeek.cs:35`, 50% branch: the
    `version is >= Ssl3Version and <= Tls12Version` lambda only ever sees one outcome per element.

## Acceptance criteria

- [ ] A data-driven `Surl.Networking.UnitTests` test calls `ForScheme` with every listed scheme and asserts its ALPN list,
      plus unlisted schemes of the same lengths as listed ones (for example `http`, `ws`, `s4`, `ocj`, `mcpx`, `ax`,
      `abfsx`, `gx`, `grpcx`, `xmppx`) and `""`, each asserting the empty list.
- [ ] Named tests peek ClientHellos whose `supported_versions` hold, beside TLS 1.3, a version below SSL 3.0
      (for example `0x0200`), a version inside SSL 3.0..TLS 1.2, and only versions above TLS 1.2 (for example a GREASE
      value), so the lambda sees each side of both comparisons; each asserts `OffersOnlyTls13`.
- [ ] `Measure-CodeQuality.ps1 -Library Surl.Networking.UnitLibrary` reports `100 | 100` with 0 failing members, and
      the fast tests are green.

## Notes

- Tests only (test-writer). If a string-switch edge proves unreachable by any input, say so in the Notes and reshape
  only that method, keeping every scheme's answer.

## Log

- 2026-11-17: Created.
