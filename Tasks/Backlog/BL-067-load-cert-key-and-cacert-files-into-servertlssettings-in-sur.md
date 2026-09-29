---
id: BL-067
title: Load --cert, --key and --cacert files into ServerTlsSettings in Surl.Networking
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-012, BL-063]
touches: [Surl.Networking.UnitLibrary, Surl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-067 — Load --cert, --key and --cacert files into ServerTlsSettings in Surl.Networking

## Goal

`Surl.Networking.UnitLibrary` reads the files `--cert`, `--cert-type`, `--key`,
`--key-type`, `--pass` and `--cacert` name, in the formats ADR-0010 section 3 and 5
allow, into the certificate, intermediates and trust anchors `ServerTlsSettings` takes,
and reports every failure as a typed failure the composition root maps to
`CertificateProblem` (58), `CaCertificateBadFile` (77) or `FailedInit` (2).

## Context

- ADR-0010, sections 3 and 5, is the specification: `PEM` (certificate blocks, first is
  the server's, key from `--key` or the same file), `DER` (one certificate, `--key`
  required), `P12` (`--key` refused); keys PKCS#8, encrypted PKCS#8 (`--pass`), PKCS#1,
  SEC1 in PEM, PKCS#8 or encrypted PKCS#8 in DER; RSA 2048+ and ECDSA P-256/384/521
  served, Ed25519/Ed448 are `CertificateProblem`; `--cacert` PEM (one or more blocks) or
  one DER certificate.
- BL-012 built `ServerTlsSettings(certificate, intermediates, clientTrustAnchors,
  timeProvider)`. Its certificate needs an exportable key (it is re-imported through
  PKCS#12 for Schannel): load PKCS#12 with `X509KeyStorageFlags.Exportable`.
- `Surl.Networking` never picks a `SurlExitCode` (ADR-0004 section 6): throw an exception
  carrying a failure kind, as `ListenerBindException` does, and let `Surl.Console` map it.
  BL-064 adds the two exit codes.
- Files are read at startup, before any listener binds. Tests write generated
  certificates and keys under `Path.GetTempPath()` and delete them; nothing is committed.

## Acceptance criteria

- [ ] A loader in `Surl.Networking.UnitLibrary` reads each format and key encoding
      ADR-0010 section 3 lists, and fast tests prove each with a generated certificate.
- [ ] A missing, unreadable or wrong-format `--cert` or `--key`, a key that does not match,
      a missing or wrong `--pass` and an Ed25519 key each give the failure kind that maps
      to `CertificateProblem`, pinned by a test.
- [ ] A `--cacert` file that exists but holds no certificate gives the kind that maps to
      `CaCertificateBadFile`; one that does not exist gives the kind that maps to
      `FailedInit`; both pinned by tests.
- [ ] `dotnet build Surl.Networking.UnitLibrary -warnaserror` is clean, the fast tests
      are green, and `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Networking.UnitLibrary`.

## Notes

Filed by BL-012, which built the handshake over `ServerTlsSettings` and left reading the
option files to this task (ADR-0010's "Consequences" had grouped both under BL-012).

## Log

- 2026-09-28: Created.
