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
completed: 2026-09-28
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

- [x] A loader in `Surl.Networking.UnitLibrary` reads each format and key encoding
      ADR-0010 section 3 lists, and fast tests prove each with a generated certificate.
- [x] A missing, unreadable or wrong-format `--cert` or `--key`, a key that does not match,
      a missing or wrong `--pass` and an Ed25519 key each give the failure kind that maps
      to `CertificateProblem`, pinned by a test.
- [x] A `--cacert` file that exists but holds no certificate gives the kind that maps to
      `CaCertificateBadFile`; one that does not exist gives the kind that maps to
      `FailedInit`; both pinned by tests.
- [x] `dotnet build Surl.Networking.UnitLibrary -warnaserror` is clean, the fast tests
      are green, and `Measure-CodeQuality.ps1` reports no failing member in
      `Surl.Networking.UnitLibrary`.

## Notes

Filed by BL-012, which built the handshake over `ServerTlsSettings` and left reading the
option files to this task (ADR-0010's "Consequences" had grouped both under BL-012).

Delivered (2026-09-28): `ServerCertificateFileLoader.Load(certPath, ServerCertificateFormat,
keyPath, ServerKeyFormat, passphrase)` returns a `LoadedServerCertificate`;
`ClientTrustAnchorFileLoader.Load(path)` returns the anchors; failures are
`TlsFileLoadException` with `TlsFileLoadFailure` `ServerCertificateUnusable` (-> 58),
`CaCertificateNotFound` (-> 2) or `CaCertificateUnreadable` (-> 77). 47 new fast tests in
`ServerCertificateFileLoaderTests` and `ClientTrustAnchorFileLoaderTests`; Networking
fast tests 206 green; `Measure-CodeQuality.ps1 -Library Surl.Networking.UnitLibrary`:
0 failing members.

Choices made within ADR-0010 (sensible defaults, no new ADR needed - each follows from
sections 3 and 5):
- The loader takes the `--cert-type`/`--key-type` words as enums; parsing the words, and
  refusing `--key` with `P12` (`FailedInit`), stays with the command line. Given a key path
  with `P12` anyway, the loader throws `ArgumentException` (a caller bug, not a file problem).
- A `DER` `--cert` without `--key` is `ServerCertificateUnusable`: the key is missing, which
  ADR-0010 lists under `CertificateProblem`, and it is not among the `FailedInit` cases.
- The key algorithm comes from the certificate: RSA of 2048 bits or more and ECDSA on
  P-256/384/521 (checked by the named-curve OID in the certificate, platform-neutral) are
  served; any other algorithm (Ed25519, Ed448) or curve is `ServerCertificateUnusable`
  before the key is read. A key of another type than the certificate is a mismatch.
- A `DER` key is encrypted PKCS#8 when its outer sequence starts with a sequence, PKCS#8
  otherwise, so `--pass` given for a plain key is ignored, as curl ignores it.
- `DER` means strictly one DER value: `X509CertificateLoader.LoadCertificate` also accepts
  PEM, so a PEM file named `DER` is refused by an ASN.1 check first.
- `--cacert`: "does not exist" means neither a file nor a directory exists at the path
  (so an empty path is `CaCertificateNotFound`); a directory or any read failure of an
  existing path is `CaCertificateUnreadable`. PEM `CERTIFICATE` blocks win; with none, one
  DER certificate is tried.
- P12 is loaded `Exportable` with `ServerCertificateImport.KeyStorageFlagsFor`, so
  `ServerTlsSettings` can re-import it; PEM/DER keys are joined with `CopyWithPrivateKey`,
  which also rejects a non-matching key. Tests prove each path by building a
  `ServerTlsSettings` from the result.
- Ed25519 cannot be generated by the BCL in .NET 10, so the tests hand-build an RFC 8410
  public key and PKCS#8 key and an RSA-signed certificate around the public key.

Follow-up: calling the loaders from `Surl.Console` and mapping the failures to exit codes
is BL-038's (it already depends on BL-064 and this task); no new task filed.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Surl.Networking loads --cert (PEM, DER, P12), --key (PKCS#8, encrypted PKCS#8, PKCS#1, SEC1) and --cacert into ServerTlsSettings inputs, with typed TlsFileLoadFailure for exit codes 58, 77 and 2
