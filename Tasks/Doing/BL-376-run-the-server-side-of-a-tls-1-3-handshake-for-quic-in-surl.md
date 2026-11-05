---
id: BL-376
title: Run the server side of a TLS 1.3 handshake for QUIC in Surl.Tls13
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-375]
touches: [Surl.Tls13.UnitLibrary, Surl.Tls13.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-376 — Run the server side of a TLS 1.3 handshake for QUIC in Surl.Tls13

## Goal

`Surl.Tls13` runs the server side of a TLS 1.3 handshake for QUIC. Given the client's handshake bytes,
it returns the server's handshake bytes per encryption level (Initial, Handshake) and the traffic
secrets for each level. It chooses parameters as BL-372's ADR decides, signs `CertificateVerify` with
surl's certificate key, and checks the client's Finished.

## Context

- Decision: BL-372's ADR, decision 3: the cipher suites, groups and signature algorithms, and ALPN
  `h3` only.
- RFC 8446 section 4.1.1 (negotiation), 4.1.4 (HelloRetryRequest when the client's key share does not
  match a supported group), 4.4.3 (CertificateVerify content), and 4.4.4 (Finished). RFC 9001
  section 4 (how QUIC uses the handshake: no `EndOfEarlyData`, no `ChangeCipherSpec`, and alert
  `no_application_protocol` when ALPN does not match, section 8.1).
- Key exchange: X25519 through `Surl.Cryptography.Curve25519` (ADR-0048; the reference needs the ADR's
  ADR-0002 row), and secp256r1 and secp384r1 through the BCL's `ECDiffieHellman`. Signatures use the
  BCL's `RSA` (PSS) and `ECDsa` on the certificate key, which comes from the same certificate source
  as `https` (ADR-0010; `--self-signed` per ADR-0032).
- Builds on BL-375's messages and key schedule. Time comes from an injected `TimeProvider` if it is
  needed. The library never touches a socket.

## Acceptance criteria

- [ ] Fed RFC 8448 section 3's ClientHello, with that trace's server key share and random injected,
      the handshake produces the trace's server flight and secrets, except for the signature, which a
      test verifies with the certificate's public key.
- [ ] BL-363's recorded ClientHello from each pinned build completes a handshake whose transcript a
      test-side TLS 1.3 client written in the test project verifies.
- [ ] Named tests cover a HelloRetryRequest for an unsupported key share, `no_application_protocol`
      without `h3`, `handshake_failure` when no cipher suite is shared, and a client Finished that
      does not verify (`decrypt_error`).
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
