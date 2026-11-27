---
id: BL-1958
title: Name the SMTP STARTTLS tests after a TLS upgrade being available, not a certificate
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitTests/SmtpStartTlsTests.cs, Protocols/Mail/Surl.Protocol.Smtp.UnitTests/SmtpCommandTests.cs, Protocols/Mail/Surl.Protocol.Smtp.UnitTests/SmtpTestExchange.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-1958 — Name the SMTP STARTTLS tests after a TLS upgrade being available, not a certificate

## Goal

The names and comment in the SMTP `STARTTLS` tests say what the tests vary: whether a TLS upgrade is available.

## Context

Found while working BL-1952 (2026-11-27), which did the same for LDAP. `SmtpProtocolServer` is told only
`isTlsUpgradeAvailable` (a certificate or TLS-SRP; ADR-0122 decision 5, name per ADR-0069), never a certificate. All
files are in `Protocols/Mail/Surl.Protocol.Smtp.UnitTests/`; line numbers are as of that date and approximate.

- `SmtpStartTlsTests.cs`, lines 19, 27 and 75: `ServeAsync_EhloOnPlaintextWithACertificate_AdvertisesStartTlsLast`,
  `ServeAsync_EhloWithoutACertificate_DoesNotAdvertiseStartTls`,
  `ServeAsync_StartTlsWithoutACertificate_Answers454AndStaysPlaintext`.
- `SmtpCommandTests.cs`, line 35: `DisplayName = "STARTTLS with no certificate"`.
- `SmtpTestExchange.cs`, line 43: the `StartTlsServer` summary "A server with a certificate".

Use the LDAP shape: `...WithTlsUpgradeAvailable_...`, `...WithTlsUpgradeUnavailable_...`, "with a TLS upgrade
unavailable". Names and comments only; no store, event or fault is involved.

## Acceptance criteria

- [ ] No test method name or `DisplayName` in `SmtpStartTlsTests.cs` or `SmtpCommandTests.cs` says "Certificate" or "certificate"; each says TLS upgrade available or unavailable.
- [ ] The `SmtpTestExchange.StartTlsServer` summary says the server has a TLS upgrade available, not a certificate.
- [ ] The diff changes only those names and that comment.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
