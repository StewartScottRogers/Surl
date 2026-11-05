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
completed: 2026-11-04
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

- [x] Fed RFC 8448 section 3's ClientHello, with that trace's server key share and random injected,
      the handshake produces the trace's server flight and secrets, except for the signature, which a
      test verifies with the certificate's public key.
- [x] BL-363's recorded ClientHello from each pinned build completes a handshake whose transcript a
      test-side TLS 1.3 client written in the test project verifies.
- [x] Named tests cover a HelloRetryRequest for an unsupported key share, `no_application_protocol`
      without `h3`, `handshake_failure` when no cipher suite is shared, and a client Finished that
      does not verify (`decrypt_error`).
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100% line
      and branch coverage.

## Notes

- Shape: `TlsServerHandshake.Receive(TlsEncryptionLevel, bytes)` buffers each level's bytes (a
  message may span `CRYPTO` frames and calls) and returns `TlsHandshakeOutput` (Initial bytes,
  Handshake bytes, handshake and application secrets as they become known). Options are
  `TlsServerHandshakeOptions` (chain, key, ALPN list, transport parameters, extra
  EncryptedExtensions first). The server's application secrets come with its flight (RFC 9001
  lets the server send 1-RTT after its Finished); the client Finished is checked in constant time.
- Choices within ADR-0082 decision 3, taken as sensible defaults (no new ADR: each applies a rule
  the ADR or the RFC already states): ALPN in the server's order (only `h3` today); an empty ALPN
  list or null transport parameters turns those checks off, used only by the RFC 8448 test, whose
  trace is TLS over TCP; extension order in ServerHello and HelloRetryRequest follows RFC 8448
  (key_share, then supported_versions); no cookie (QUIC has Retry); a second ClientHello that
  changes the suite or does not send exactly one share for the retry group is
  `illegal_parameter` (RFC 8446 sections 4.1.4, 4.2.8); a message over 128 KiB is
  `illegal_parameter` (a bound for ADR-0006; the largest ClientHello curl sends is 1519 bytes,
  and X25519MLKEM768 shares fit easily); bytes at the 1-RTT level are `unexpected_message`.
- NIST points: Windows' CNG reports an off-curve point as `PlatformNotSupportedException`, other
  platforms as `CryptographicException`, so `TlsNistCurve` checks y^2 = x^3 - 3x + b mod p
  itself with `BigInteger` before the BCL sees it; the curve constants are FIPS 186-5's, and the
  valid-point tests on all three curves would fail if one were wrong.
- RFC 8448 test: the trace's server random and x25519 private key are injected through
  `UnitTestScriptedRandom`, and its RSA key (section 2) signs. ServerHello, EncryptedExtensions,
  Certificate and both handshake traffic secrets match the trace byte for byte; the RSA-PSS
  signature is randomised, so it is verified with the certificate's public key, and the
  Finished and application secrets that follow from it are checked by the test-side client.
  The keys were copied from rfc8448.txt (downloaded to %TEMP%) with PowerShell.
- Recorded ClientHellos: the test-side client `UnitTestTlsClient` (RFC 8446 sections 4.4 and 7.1,
  its own HKDF-Expand-Label, sharing nothing with `TlsKeySchedule`) replaces the 32 bytes of each
  recording's x25519 share with its own public key, so it can derive every secret itself; every
  other byte is the pinned build's. All three get TLS_AES_256_GCM_SHA384, x25519, ALPN `h3`, and
  complete.
- Not built here, filed: BL-600 (CertificateRequest and client certificates, ADR-0010 section 5).
  X25519MLKEM768 is BL-544 and the other groups BL-545, as ADR-0082 says.
- Measured: 112 tests in Surl.Tls13.UnitTests (49 new); library line 100%, branch 100%, worst
  CRAP 10 (`Measure-CodeQuality.ps1 -Library Surl.Tls13.UnitLibrary`); `dotnet build
  -warnaserror` clean; `dotnet format --verify-no-changes` clean; fast tests green in all 43 test
  assemblies.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing.
- 2026-11-04: Doing -> Done. Surl.Tls13 runs the server side of a TLS 1.3 handshake for QUIC: per-level flights and secrets, HelloRetryRequest, CertificateVerify signing and client Finished checking, verified against RFC 8448 and three recorded upstream curl ClientHellos
