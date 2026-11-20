---
id: BL-1454
title: Route every ClientHello offering TLS 1.3 to Surl.Tls13Stream when the server accepts only TLS 1.3 and SslStream cannot serve it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1453]
touches: [Foundation/Surl.Networking.UnitLibrary, Foundation/Surl.Networking.UnitTests]
requirement: none
created: 2026-11-20
completed:
---
# BL-1454 — Route every ClientHello offering TLS 1.3 to Surl.Tls13Stream when the server accepts only TLS 1.3 and SslStream cannot serve it

## Goal

On macOS, `surl --tlsv1.3 --cert server.pem --key server.key https://127.0.0.1:0/` completes a
handshake with `curl --cacert server.pem https://...` (which offers TLS 1.2 and 1.3) through
`Surl.Tls13Stream`, as BL-1453's amendment to ADR-0157 says.

## Context

- Surly bug report `2026-11-19T2203-Surl-bug-report.md` item 3: curl exit 35 on `macos-latest`.
- `Foundation/Surl.Networking.UnitLibrary/ServerTlsHandshake.cs:78-79` chooses the hand-built path when
  `settings.CanServeTls13Stream && tls13Support.SslStreamLacksTls13Server`, and `:114-116` sends the
  hello to it only when `peek.OffersOnlyTls13` (`ClientHelloPeek.cs:32-35`). With the settings' lowest
  accepted version at TLS 1.3 (`ServerTlsSettings.AcceptedVersions`, a `TlsVersionRange`), a hello that
  lists TLS 1.3 among others must take the hand-built path too. A hello without TLS 1.3 gets what the
  amendment says.
- `ClientHelloBytes` (test helper) and a fake `ITls13Support` let this run on Windows and Linux lanes;
  `PlatformTls13Support` is true only on macOS.

## Acceptance criteria

- [ ] With a fake `ITls13Support` that lacks a TLS 1.3 server and settings accepting TLS 1.3 only, a
      ClientHello offering TLS 1.2 and 1.3 completes through `Tls13ServerStream`, and `TlsSession`
      reports `SslProtocols.Tls13` (named test in `Foundation/Surl.Networking.UnitTests`).
- [ ] With the same settings, a hello offering only TLS 1.2 is answered as the amendment says (named
      test).
- [ ] With the default range (1.2 to 1.3), a hello offering 1.2 and 1.3 still goes to `SslStream`, as
      ADR-0157 decision 1 says (existing tests green).
- [ ] `dotnet build Foundation/Surl.Networking.UnitLibrary -warnaserror` is clean, the fast tests are
      green, and coverage stays at 100% line and branch.

## Notes

- The `--cacert` case is the separate task BL-1453 files.

## Log

- 2026-11-20: Created.
- 2026-11-20: Filed from `Z:\repos\Surly\Documentation\Bug-Reports\2026-11-19T2203-Surl-bug-report.md` item 3.
