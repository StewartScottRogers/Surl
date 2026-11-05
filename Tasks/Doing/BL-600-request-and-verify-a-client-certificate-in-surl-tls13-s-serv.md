---
id: BL-600
title: Request and verify a client certificate in Surl.Tls13's server handshake
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-376]
touches: [Surl.Tls13.UnitLibrary, Surl.Tls13.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-600 — Request and verify a client certificate in Surl.Tls13's server handshake

## Goal

`TlsServerHandshake` sends a CertificateRequest when its options ask for a client certificate,
and reads and verifies the client's Certificate and CertificateVerify before its Finished.

## Context

- ADR-0082 decision 3, "What is not built": client certificates follow ADR-0010 section 5 - the
  server sends `CertificateRequest` exactly when it would over TCP.
- BL-376 built the handshake without it: at the Handshake level it accepts only the client's
  Finished (`TlsServerHandshake.ReceiveClientFinished`). `CertificateRequest` already writes
  itself (BL-375).
- RFC 8446 sections 4.3.2 (CertificateRequest), 4.4.2 and 4.4.3 (the client's Certificate and
  CertificateVerify, context string "TLS 1.3, client CertificateVerify"), and 4.4.2.4 (an empty
  client Certificate: `certificate_required` when one is required).
- Expected values come from RFC 8448 section 4 (Client Authentication) and from the
  specification, never from the Curl port (ADR-0003).

## Acceptance criteria

- [ ] With a client certificate requested, the server flight carries CertificateRequest between
      EncryptedExtensions and Certificate (RFC 8446 section 4.4), and a
      test-side client's Certificate, CertificateVerify and Finished complete the handshake.
- [ ] Named tests cover an empty client Certificate when one is required (`certificate_required`),
      a client CertificateVerify that does not verify (`decrypt_error`), and a signature scheme the
      CertificateRequest did not offer (`illegal_parameter`).
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100%
      line and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
