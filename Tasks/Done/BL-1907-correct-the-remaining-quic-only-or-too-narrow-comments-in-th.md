---
id: BL-1907
title: Correct the remaining QUIC only or too narrow comments in the Tls13 library and tests
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-1895]
touches: [Cryptography/Surl.Tls13.UnitLibrary/TlsAlertException.cs, Cryptography/Surl.Tls13.UnitLibrary/TlsServerHandshakeOptions.cs, Cryptography/Surl.Tls13.UnitTests/Rfc8448.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1907 — Correct the remaining QUIC only or too narrow comments in the Tls13 library and tests

## Goal

The comments on `TlsAlertException`, `TlsServerHandshakeOptions.ApplicationProtocols` and the test class `Rfc8448`
are true of the code and tests as they are.

## Context

Found by BL-1894 on 2026-11-27. Comment-only changes; no store, event or fault changes. `Surl.Tls13Stream` also
drives this handshake, for TLS 1.3 over TCP (ADR-0157). BL-1895 covers `TlsAlert.cs` and `TlsServerHandshake.cs`
(including its randomness remarks), so those files are not part of this task.

- `Cryptography/Surl.Tls13.UnitLibrary/TlsAlertException.cs` justifies the caller reporting the alert only by "TLS
  over QUIC sends no alert record". Over TCP the caller, `Tls13ServerStream`, writes a fatal alert record.
- `Cryptography/Surl.Tls13.UnitLibrary/TlsServerHandshakeOptions.cs`, `ApplicationProtocols`, gives only the HTTP/3
  case. QUIC also serves DNS over QUIC (`doq`), and TCP passes the listener's list. BL-1906 changes what a hello with
  no ALPN gets; describe `TlsServerNegotiation.ChooseApplicationProtocol` as it is when this task runs.
- `Cryptography/Surl.Tls13.UnitTests/Rfc8448.cs` says every expected value in the project's RFC tests comes from that
  file. The "expected values" list at the end of the library's `CLAUDE.md` names other sources too.

## Acceptance criteria

- [x] `TlsAlertException`'s summary says the caller reports the alert, over QUIC as a connection error and over TCP as
      an alert record written by `Surl.Tls13Stream`.
- [x] `ApplicationProtocols`' summary covers the QUIC lists (`h3`, `doq`) and the TCP listener's list, and says what an
      empty list, a hello with no ALPN and a hello with nothing in common get, matching
      `TlsServerNegotiation.ChooseApplicationProtocol`.
- [x] `Rfc8448`'s summary describes only what that file holds and makes no claim that it is the source of every
      expected value.
- [x] The diff to the three `.cs` files changes comment lines only, and `dotnet build` has no warnings.

## Notes

- 2026-11-27: BL-1905 (commit abd1724d2) had already made most of the `TlsAlertException` summary right. This task added
  that the caller reports the alert, and that over QUIC it is a connection error.
- Checked against `TlsServerNegotiation.ChooseApplicationProtocol`, `QuicServerConnection`, `DnsOverQuicProtocolServer`,
  `ServerTlsSettings.CreateTls13Options` and `TlsApplicationProtocols`. The `ApplicationProtocols` summary names `https`
  only as an example of a TCP list, so it does not repeat the per-scheme table in `Surl.Networking`.
- Gates: `dotnet build` 0 warnings, `dotnet format --verify-no-changes` clean on the three files, fast tests 30280
  passed, 0 failed, 3 skipped across 83 test assemblies.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The TlsAlertException, ApplicationProtocols and Rfc8448 comments match the code and tests
