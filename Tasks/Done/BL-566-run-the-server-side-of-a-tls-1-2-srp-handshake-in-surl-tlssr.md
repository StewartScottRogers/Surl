---
id: BL-566
title: Run the server side of a TLS 1.2 SRP handshake in Surl.TlsSrp
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-564, BL-565]
touches: [Cryptography/Surl.TlsSrp.UnitLibrary, Cryptography/Surl.TlsSrp.UnitTests, Record-CurlExchange.ps1, Run-TlsSrpRelay.cs, Documentation/Planning/Decisions/ADR-0122-how-surl-serves-https-over-tls-srp.md]
requirement: none
created: 2026-11-04
completed: 2026-11-05
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

- [x] A named test replays the recorded upstream curl 8.21.0 ClientHello and client flight with a
      fixed server random and `b`, and the server's flight matches the expected bytes the test
      computes; application data then round-trips.
- [x] Named tests cover: an unknown user, a wrong password (client Finished fails to verify,
      `decrypt_error` or the ADR's alert), a ClientHello without the `srp` extension, no shared
      cipher suite (`handshake_failure`), an out-of-order handshake message
      (`unexpected_message`), and a client `A` with `A % N == 0` (`illegal_parameter`).
- [x] `dotnet build -warnaserror` is clean, the fast tests are green, and the library keeps 100%
      line and branch coverage, complexity of at most 10 per method and CRAP of at most 30.

## Notes

- Built: `TlsSrpServer.AuthenticateAsync(Stream)` returns a `TlsSrpSession` whose
  `TlsSrpStream` carries the plaintext (ADR-0122 decision 5's shape). It is backed by
  `TlsClientHello`, `TlsSrpNegotiation`, `TlsSrpAccounts` (unknown users are simulated),
  `TlsServerMessages`, `TlsHandshakeMessageReader` and `TlsAlerts`. The library now references
  `Surl.Cryptography.Srp.UnitLibrary`, as ADR-0122 decision 1 allows.
- Measured: `Record-CurlExchange.ps1` gained `-TlsSrp`, which starts the new root file-based app
  `Run-TlsSrpRelay.cs` as the TLS-SRP relay (Surl.TlsSrp's own server, a fixed salt and random,
  capturing both directions). Both files were added to `touches` on 2026-11-05: measuring needed
  them, and no task in Doing names them. Against the static 8.21.0 OpenSSL build: `jsmith`/`abc`
  completes (`C0 20`, EMS, ETM, ALPN `http/1.1`, exit 0); `jsmith`/`badpass` and
  `baduser`/`badpass` exit 35 on `bad_record_mac`; without `-k`, exit 60. These are upstream cases
  320, 322, 321 and 324's outcomes. The fixtures are in `Surl.TlsSrp.UnitTests/Fixtures`.
- Found: a salt that starts with a zero byte breaks OpenSSL's client, which hashes `s` as a
  number. Recorded in ADR-0122 Amendment 1 (added to `touches` for this reason) and in BL-567's
  Notes.
- Choice: only the three unsigned suites are served. The RSA- and DSS-signed suites with a
  `Certificate` message are filed as BL-907, because none of the upstream cases needs them.
- Choice: `TlsSrpStream` is asynchronous only, and any client alert during the handshake ends it
  with no reply (ADR-0122 Amendment 1).
- Choice: a ServerHello with nothing to answer leaves its extensions block out (RFC 5246 section
  7.4.1.3).
- Gates: 181 tests in `Surl.TlsSrp.UnitTests`. `Measure-CodeQuality.ps1 -Library
  Surl.TlsSrp.UnitLibrary` reports 100% line and 100% branch coverage, with worst CRAP 10.

## Log

- 2026-11-04: Created.
- 2026-11-05: Backlog -> Doing.
- 2026-11-05: Doing -> Done. Surl.TlsSrp completes the server side of a TLS 1.2 SRP handshake against pinned upstream curl 8.21.0 and carries application data; failures send ADR-0122's alerts
