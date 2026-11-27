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
completed:
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

- [ ] `TlsAlertException`'s summary says the caller reports the alert, over QUIC as a connection error and over TCP as
      an alert record written by `Surl.Tls13Stream`.
- [ ] `ApplicationProtocols`' summary covers the QUIC lists (`h3`, `doq`) and the TCP listener's list, and says what an
      empty list, a hello with no ALPN and a hello with nothing in common get, matching
      `TlsServerNegotiation.ChooseApplicationProtocol`.
- [ ] `Rfc8448`'s summary describes only what that file holds and makes no claim that it is the source of every
      expected value.
- [ ] The diff to the three `.cs` files changes comment lines only, and `dotnet build` has no warnings.

## Notes

## Log

- 2026-11-27: Created.
