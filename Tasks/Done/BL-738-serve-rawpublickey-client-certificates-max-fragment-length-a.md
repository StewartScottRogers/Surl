---
id: BL-738
title: Serve RawPublicKey, client certificates, max_fragment_length and the renegotiation refusal in Surl.Dtls
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-414, BL-415]
touches: [Cryptography/Surl.Dtls.UnitLibrary, Cryptography/Surl.Dtls.UnitTests, Documentation/Planning/Decisions/ADR-0087-how-surls-dtls-1-2-server-handshakes-for-coaps.md]
requirement: none
created: 2026-11-05
completed: 2026-11-05
---
# BL-738 — Serve RawPublicKey, client certificates, max_fragment_length and the renegotiation refusal in Surl.Dtls

## Goal

`DtlsServerSession` also answers the parts of ADR-0087 that BL-414's certificate handshake left out: RawPublicKey
mode, client certificates under `--cacert`, `max_fragment_length`, and a hello on an established association.

## Context

- ADR-0087 decision 3 (RawPublicKey per RFC 7250; client certificates per ADR-0010 section 5, with
  `CertificateRequest` and `CertificateVerify`), decision 5 (`max_fragment_length`, RFC 6066 section 4; the
  `server_certificate_type` and `client_certificate_type` extensions), decision 6 (an epoch 1 `ClientHello` gets a
  warning `no_renegotiation`; an epoch 0 hello with a valid cookie on an established association starts a new
  handshake that replaces the old one once it completes, RFC 6347 section 4.2.8), decision 10 (the alerts:
  `unsupported_certificate`, `bad_certificate`, `unknown_ca`, `certificate_expired`, `decrypt_error`).
- BL-414 built the certificate handshake: `DtlsServerSession`, `DtlsServerNegotiation`, `DtlsAssociationRecords`.
  The message codecs already exist: `DtlsRawPublicKeyCertificate`, `DtlsCertificateRequest`, `DtlsCertificateVerify`.
- The test client is `UnitTestDtlsClient` in `Surl.Dtls.UnitTests`; extend it from the RFCs.
- Filed by BL-414, which kept to its acceptance criteria.

## Acceptance criteria

- [x] Named tests show: a RawPublicKey handshake completed when the client lists `RawPublicKey` first; a list
      naming neither type ending with `unsupported_certificate`; with a CA configured, a client certificate that
      chains completing the handshake and one that does not ending with `unknown_ca`, an expired one with
      `certificate_expired` by the `TimeProvider`, a bad `CertificateVerify` with `decrypt_error`;
      `max_fragment_length` echoed and every server record within it; an epoch 1 `ClientHello` answered with a
      warning `no_renegotiation` while application data still flows.
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line and branch
      coverage.

## Notes

- Touches widened by ADR-0087's file: the details this task decided are recorded there as the
  "Amendment (2026-11-05, BL-738)". No task in Doing named it.
- Built: `DtlsServerNegotiation` answers `server_certificate_type`, `client_certificate_type` and
  `max_fragment_length`; `DtlsServerOptions.ClientCertificateAuthorities` (the `--cacert` anchors)
  turns on `CertificateRequest`; the new internal `DtlsClientCertificate` checks the chain by the
  `TimeProvider` and the `CertificateVerify` signature; `DtlsAssociationRecords.MaximumPlaintextLength`
  cuts handshake fragments; `DtlsServerSession.MaximumApplicationDataLength` bounds `SendAsync`; an
  epoch 1 `ClientHello` gets a warning `no_renegotiation`.
- Decided (ADR-0087 amendment): a client key neither RSA nor ECDSA is `unsupported_certificate`; a
  `CertificateVerify` scheme that does not fit the key is `decrypt_error`; a `max_fragment_length` not one
  byte is `decode_error`; application data over the limit is refused with `ArgumentOutOfRangeException`
  rather than split, because CoAP carries one message per record.
- Tests: `DtlsServerSessionCertificateModesTests`, `DtlsServerSessionFragmentLengthAndRenegotiationTests`,
  `DtlsClientCertificateTests`; `UnitTestDtlsClient` now sends the certificate type and fragment length
  extensions, a client `Certificate` and `CertificateVerify`. Client certificates are issued by an
  in-memory test CA around `ManualTimeProvider`'s clock, so validity is judged by the session's clock.
- Coverage of `Surl.Dtls.UnitLibrary` measured with the Dtls tests alone: line 100%, branch 100%. The
  state machine's expected message per state is a static table, so it adds no untestable switch branches.
- Follow-up filed: BL-773, an epoch 0 hello with a valid cookie replacing an established association
  (ADR-0087 decision 6, third bullet), which the acceptance criteria here do not name.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.Dtls serves RawPublicKey, client certificates under --cacert, max_fragment_length and the no_renegotiation refusal
