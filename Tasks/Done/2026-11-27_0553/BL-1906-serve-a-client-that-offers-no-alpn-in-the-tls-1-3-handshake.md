---
id: BL-1906
title: Serve a client that offers no ALPN in the TLS 1.3 handshake over TCP
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1894]
touches: [Cryptography/Surl.Tls13.UnitLibrary, Cryptography/Surl.Tls13.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1906 — Serve a client that offers no ALPN in the TLS 1.3 handshake over TCP

## Goal

Surl's hand-built TLS 1.3 handshake over TCP completes for a ClientHello with no ALPN extension, agreeing no protocol,
while QUIC keeps refusing it.

## Context

Found by BL-1894 on 2026-11-27 by reading code; **not measured** with upstream curl (`--no-alpn` on `https` with
TLS 1.3, on the path that uses `Surl.Tls13Stream`). `--no-alpn` is listed in https://curl.se/docs/manpage.html
(page for curl 8.23.1).

- `Cryptography/Surl.Tls13.UnitLibrary/TlsServerNegotiation.cs`, `ChooseApplicationProtocol`, throws
  `no_application_protocol` for a hello with no ALPN whenever the server's list is not empty.
- Over TCP, `ServerTlsSettings.CreateTls13Options` (`Surl.Networking`) passes the listener's list, and leaves
  `QuicTransportParameters` null.
- ADR-0010 section 4 decides that a client offering no ALPN is served without it. The `SslStream` path does that
  (`UpgradeToTlsAsync_ClientOffersNoAlpn_ServesWithoutIt` in
  `Foundation/Surl.Networking.UnitTests/StreamConnectionTlsTests.cs`).
- QUIC must keep refusing (RFC 9001 section 8.1). `QuicServerConnection` always sets `QuicTransportParameters`, and
  `RequireExtensions` already uses it to tell QUIC apart, so the fix needs no new option and no change outside the
  library: serve without ALPN only when the hello has no ALPN extension and `QuicTransportParameters` is null.
- Out of scope: an ALPN list offered with nothing in common over TCP stays `no_application_protocol`. ADR-0010's
  platform table (section 6, "ALPN offered but no protocol in common") records that `SslStream` differs by platform
  there; this task does not change it.
- The TCP path serves only where `PlatformTls13Support` says `SslStream` has no TLS 1.3 server (macOS), so the unit
  tests are the proof. The expected behaviour comes from ADR-0010 section 4 and RFC 7301; nothing written by this
  task claims an upstream curl measurement unless one is made and recorded in Notes.
- Stand-in: no store, event, option or fault changes. A test client that sends no ALPN gets the same answer from
  both TCP handshake paths.

## Acceptance criteria

- [x] `Receive_NoAlpnAndNoQuicTransportParameters_ServesWithoutAlpn` in `TlsServerHandshakeTests` fails before the
      fix and passes after it: with the server list `h2`, `http/1.1`, null `QuicTransportParameters` and a hello with
      no ALPN and no `quic_transport_parameters`, `Receive` returns the server's flight, `ApplicationProtocol` is null
      and EncryptedExtensions carries no ALPN extension (type `0x0010`).
- [x] `Receive_ClientHelloWithoutAlpn_FailsWithNoApplicationProtocol` (QUIC options) and
      `Receive_OptionsNameDoqAndTheClientOffersOnlyH3_FailsWithNoApplicationProtocol` (a list with nothing in common,
      no transport parameters) pass unchanged.
- [x] The library `CLAUDE.md`'s QUIC and TCP `TlsServerHandshakeOptions` bullets and the `no_application_protocol`
      alert table row say that a hello with no ALPN is refused over QUIC and served without ALPN over TCP.
- [x] The XML docs on `TlsServerNegotiation.Choose` and `TlsServerHandshakeOptions.ApplicationProtocols` say the same.
- [x] `dotnet build -warnaserror` is clean, and `Surl.Tls13.UnitTests`, `Surl.Tls13Stream.UnitTests` and
      `Surl.Quic.UnitTests` pass with `--filter "TestCategory!=Integration"`.
- [x] `Surl.Tls13.UnitLibrary` keeps 100% line and branch coverage.

## Notes

- Fix: `TlsServerNegotiation.ChooseApplicationProtocol` returns null when the hello has no ALPN extension and
  `QuicTransportParameters` is null. No new option, no change outside the library. No new decision, so no ADR:
  ADR-0010 section 4 already decides the behaviour.
- The new test failed before the fix with `TlsAlertException` ("The client offers no ALPN protocol the server agrees
  (h2, http/1.1)."), and passes after it.
- Gates: `dotnet build -warnaserror` clean (0 warnings); all fast tests passed; `Surl.Tls13.UnitTests` 225,
  `Surl.Tls13Stream.UnitTests` 48, `Surl.Quic.UnitTests` 514; format check clean on the two changed projects;
  `Surl.Tls13.UnitLibrary` line-rate 1, branch-rate 1 (cobertura).
- Pipeline: the change is a few lines and the task names the plan and the test, so planning, test, implementation and
  docs were done in the session; review was by `code-reviewer` (no Must fix; its one Should fix, qualifying the
  `Choose` remarks with "when the server's ALPN list is not empty", is applied).
- Conformance stage skipped: **no upstream curl measurement was made**. This path serves only where `SslStream` has no
  TLS 1.3 server (macOS), so a pinned Windows build cannot reach it.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. A TLS 1.3 ClientHello with no ALPN is served without ALPN over TCP; QUIC still refuses it
