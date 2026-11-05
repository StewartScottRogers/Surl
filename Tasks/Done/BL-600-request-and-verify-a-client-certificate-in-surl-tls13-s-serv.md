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
completed: 2026-11-04
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

- [x] With a client certificate requested, the server flight carries CertificateRequest between
      EncryptedExtensions and Certificate (RFC 8446 section 4.4), and a
      test-side client's Certificate, CertificateVerify and Finished complete the handshake.
- [x] Named tests cover an empty client Certificate when one is required (`certificate_required`),
      a client CertificateVerify that does not verify (`decrypt_error`), and a signature scheme the
      CertificateRequest did not offer (`illegal_parameter`).
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100%
      line and branch coverage.

## Notes

- Built: `TlsServerHandshakeOptions.TrustsClientCertificateChain` (a delegate over the DER chain),
  `TlsServerHandshake.ClientCertificateChain`, `CertificateMessage.ReadBody`,
  `CertificateVerify.ReadBody`, `TlsReader.ReadVector24` and `TlsCertificateVerifier`. The
  client Finished is now computed when it arrives, over a transcript that includes the client's
  Certificate and CertificateVerify; the application secrets still use the hash through the
  server's Finished (RFC 8446 section 7.1).
- Choices (sensible defaults, recorded in the library's CLAUDE.md rather than an ADR, because
  `Documentation/Planning/Decisions` is in another Doing task's `touches`; they apply ADR-0082
  decision 3 and ADR-0010 section 5 and change neither):
  - Setting the delegate means request *and* require, as ADR-0010 section 5 has one mode only.
  - Chain trust is the caller's delegate, because this library never reads a clock and ADR-0010
    builds the chain with the `TimeProvider`; a false answer, or an end-entity certificate that
    is not X.509, is `bad_certificate`.
  - Offered schemes: ECDSA P-256/P-384/P-521 and RSA-PSS (rsae) SHA-256/384/512 - every scheme
    the BCL verifies for an RSA or ECDSA certificate. ed25519 and rsa_pss_pss are not offered.
  - A scheme the CertificateRequest offered but that does not fit the certificate's key (RSA-PSS
    for an EC key, or an ECDSA curve other than the key's) is `decrypt_error`: the signature
    does not verify with that key.
  - A non-empty client certificate_request_context is `illegal_parameter`.
- Expected values: the CertificateRequest bytes are written from RFC 8446 section 4.3.2; the
  client's messages come from `UnitTestClientAuthentication`, written from sections 4.4.2-4.4.4.
  RFC 8448's client-authentication trace (section 6 - the task's "section 4" is the resumed
  0-RTT trace) is not replayed byte for byte: its CertificateRequest offers a different scheme
  list, so its transcript cannot match this server's.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Surl.Tls13's server handshake requests, requires and verifies a client certificate (Certificate, CertificateVerify, Finished)
