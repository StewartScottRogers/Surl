---
id: BL-1941
title: Except TLS-SRP from the certificate-needed rows in the Azure Storage, OCI registry and S3 conformance tables
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1934]
touches: [Documentation/Conformance/Extended/AzureStorage.md, Documentation/Conformance/Extended/OciRegistry.md, Documentation/Conformance/Extended/S3.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1941 — Except TLS-SRP from the certificate-needed rows in the Azure Storage, OCI registry and S3 conformance tables

## Goal

The Azure Storage, OCI registry and S3 conformance tables say that `az`, `abfss`, `oci` and `s3` need `--cert` or
`--self-signed` only when TLS-SRP is not served.

## Context

Found while working BL-1934 (2026-11-27), read from source. Table prose only; no behaviour change.

- Under TLS-SRP (`--tlsauthtype SRP` or `--tlsuser`) a TCP listen URL that is TLS from the first byte is served with no
  certificate: `ServerTlsComposition.IsSecuredByTlsSrp` lifts the requirement from every listener that is not QUIC or
  DTLS (ADR-0122 decision 5 and Amendment 5). `az`, `abfss`, `oci` and `s3` are TCP schemes listed in
  `Foundation/Surl.Protocol.Abstractions.UnitLibrary/TlsSchemes.cs`.
- Rows that say `--cert` or `--self-signed` is "required (58 without)", with no exception, as of that date, in
  `Documentation/Conformance/Extended`: `AzureStorage:Surl-Tls-1` in `AzureStorage.md` (about line 178),
  `OCI:Surl-Tls-1` in `OciRegistry.md` (about line 145) and `S3:Surl-Tls-1` in `S3.md` (about line 148).
- The plaintext rows beside them (`AzureStorage:Surl-Tls-3`, `OCI:Surl-Tls-2`, `S3:Surl-Tls-2`: `--az-plaintext`,
  `--oci-plaintext` and `--s3-plaintext` serve plain HTTP/1.1 and warn on start) make no certificate claim. Keep their
  wording, and keep the plaintext options as the other way to serve without a certificate.
- BL-1936 corrects the same claim in other documents and lists none of these three files.

No store, event or fault is involved: documentation only.

## Acceptance criteria

- [ ] `AzureStorage:Surl-Tls-1`, `OCI:Surl-Tls-1` and `S3:Surl-Tls-1` each say `--cert`, `--self-signed` or TLS-SRP secures the scheme, and give 58 only for none of the three.
- [ ] Each of those three rows still says its schemes are implicit TLS with ALPN `http/1.1` only.
- [ ] The rows `AzureStorage:Surl-Tls-3`, `OCI:Surl-Tls-2` and `S3:Surl-Tls-2` are unchanged.
- [ ] The test column of every changed row is unchanged, and no other sentence in the three files says one of these schemes needs a certificate without excepting TLS-SRP.

## Notes

## Log

- 2026-11-27: Created.
