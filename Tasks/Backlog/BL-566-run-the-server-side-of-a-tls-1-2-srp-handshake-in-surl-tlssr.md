---
id: BL-566
title: Run the server side of a TLS 1.2 SRP handshake in Surl.TlsSrp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-564, BL-565]
touches: [Cryptography/Surl.TlsSrp.UnitLibrary, Cryptography/Surl.TlsSrp.UnitTests]
requirement: none
created: 2026-11-04
completed:
---
# BL-566 — Run the server side of a TLS 1.2 SRP handshake in Surl.TlsSrp

## Goal

`Surl.TlsSrp` completes the server side of a TLS 1.2 SRP handshake - ClientHello with the `srp`
extension, ServerHello, ServerKeyExchange with `N`, `g`, `s` and `B`, ServerHelloDone,
ClientKeyExchange with `A`, ChangeCipherSpec and Finished both ways - using BL-564's SRP-6a math
and BL-565's record layer, and then carries application data, as BL-563's ADR decisions 2, 3, 5 and
6 say.

## Context

- Decision: BL-563's ADR. Specification: RFC 5054 sections 2.2 (the `srp` extension, type 12), 2.5
  (message contents, `unknown_psk_identity` for an unknown user as section 2.5.1.3 says, or the
  ADR's choice), 2.6 (premaster secret), 2.7 (cipher suites); RFC 5246 sections 7.4 (handshake
  messages) and 7.4.9 (Finished); RFC 5746 if the ADR answers renegotiation info.
- Bytes: the ClientHello and every following message are measured from pinned upstream curl 8.21.0
  on Linux or macOS with `Record-CurlExchange.ps1` (`--tlsauthtype SRP --tlsuser jsmith
  --tlspassword abc`), recorded as the fixtures these tests replay; never from the Curl port.
- How the handshake hands over to the HTTP server is the ADR's decision 5 (planning assumption:
  `Surl.TlsSrp` returns an `IConnection` whose stream is the decrypted application data, so
  `Surl.Protocol.Http` runs over it unchanged). It references `Surl.Protocol.Abstractions` only if
  the ADR says so.
- Upstream test cases that wait on this (tag `curl-8_21_0`), `httptls+srp`: 320, 321, 322, 324.
  BL-568 runs them.

## Acceptance criteria

- [ ] A named test replays the recorded upstream curl 8.21.0 ClientHello and client flight with a
      fixed server random and `b`, and the server's flight matches the expected bytes the test
      computes; application data then round-trips.
- [ ] Named tests cover: an unknown user, a wrong password (client Finished fails to verify,
      `decrypt_error` or the ADR's alert), a ClientHello without the `srp` extension, no shared
      cipher suite (`handshake_failure`), an out-of-order handshake message
      (`unexpected_message`), and a client `A` with `A % N == 0` (`illegal_parameter`).
- [ ] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100%
      line and branch coverage, complexity of at most 10 per method and CRAP of at most 30.

## Notes

## Log

- 2026-11-04: Created.
