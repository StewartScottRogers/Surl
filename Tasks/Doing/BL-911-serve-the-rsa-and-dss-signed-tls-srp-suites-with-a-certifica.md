---
id: BL-911
title: Serve the RSA- and DSS-signed TLS-SRP suites with a Certificate in Surl.TlsSrp
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-566]
touches: [Cryptography/Surl.TlsSrp.UnitLibrary, Cryptography/Surl.TlsSrp.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-911 — Serve the RSA- and DSS-signed TLS-SRP suites with a Certificate in Surl.TlsSrp

## Goal

Given a server certificate with an RSA or DSA key, `Surl.TlsSrp`'s server also serves the signed
RFC 5054 suites (`C0 1B`, `C0 1E`, `C0 21` for RSA; `C0 1C`, `C0 1F`, `C0 22` for DSA). It sends a
`Certificate` message and signs the ServerKeyExchange, as ADR-0122 decision 2 says.

## Context

- Decision: ADR-0122 decision 2 (the signed suites, the `Certificate` message, the TLS 1.2
  `digitally-signed` signature over `client_random + server_random + ServerSRPParams`, the first
  scheme in the client's `signature_algorithms` the key can make: `08 04` for RSA, `04 02` for
  DSA, and SHA-1 with the key's algorithm when the extension is absent) and decision 3
  (`signature_algorithms`). See also its Amendment 1.
- BL-566 built the unsigned handshake: `TlsSrpServer`, `TlsSrpNegotiation.UnsignedSuites`,
  `TlsServerMessages.ServerKeyExchange`. The choice of suite still follows the client's order
  among the suites the server can serve.
- Bytes: measure with `Record-CurlExchange.ps1 -TlsSrp` against the supplementary static Windows
  build (OpenSSL 4.0.1). Extend `Run-TlsSrpRelay.cs` to take a certificate when this needs one.
  Never take bytes from the Curl port.

## Acceptance criteria

- [ ] With an RSA certificate, a named test replays pinned upstream curl 8.21.0's ClientHello and
      gets `C0 21`, a `Certificate` message and a ServerKeyExchange whose `rsa_pss_rsae_sha256`
      signature verifies with the certificate's public key.
- [ ] With a DSA certificate, a named test gets `C0 22` and a `dsa_sha256` signature that verifies.
- [ ] Without a certificate, the unsigned suites are still the only ones chosen (BL-566's tests
      unchanged).
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100%
      line and branch coverage, complexity of at most 10 per method and CRAP of at most 30.

## Notes

- Filed by BL-566 on 2026-11-05. BL-566 built only the unsigned suites: they are all that
  upstream's gnutls-serv and the four `httptls+srp` cases use, and the signed ones need
  certificate signing that BL-566's acceptance criteria do not cover.

## Log

- 2026-11-05: Created.
- 2026-11-11: Backlog -> Doing.
