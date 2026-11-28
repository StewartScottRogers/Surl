---
id: BL-1968
title: Name the SMTP STARTTLS tests after a TLS upgrade being available, not a certificate
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Smtp.UnitTests/SmtpStartTlsTests.cs, Protocols/Mail/Surl.Protocol.Smtp.UnitTests/SmtpCommandTests.cs, Protocols/Mail/Surl.Protocol.Smtp.UnitTests/SmtpTestExchange.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1968 — Name the SMTP STARTTLS tests after a TLS upgrade being available, not a certificate

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

- [x] No test method name or `DisplayName` in `SmtpStartTlsTests.cs` or `SmtpCommandTests.cs` says "Certificate" or "certificate"; each says TLS upgrade available or unavailable.
- [x] The `SmtpTestExchange.StartTlsServer` summary says the server has a TLS upgrade available, not a certificate.
- [x] The diff changes only those names and that comment.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Renamed three test methods in `SmtpStartTlsTests.cs` to `...WithTlsUpgradeAvailable_...` /
  `...WithTlsUpgradeUnavailable_...`, one `DisplayName` in `SmtpCommandTests.cs` to
  "STARTTLS with a TLS upgrade unavailable", and the `StartTlsServer` summary. No file in the SMTP test project says
  "certificate" now.
- The same naming in the IMAP, XMPP and AMQP 1.0 tests is already filed (BL-1969, BL-1972, BL-1973); nothing new filed.
- Verified 2026-11-27: `dotnet build -warnaserror` 0 warnings, `dotnet format --verify-no-changes` clean, fast tests
  green (SMTP 298 passed). No integration-level code changed, so `Integration` tests were not run.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. SMTP STARTTLS test names, DisplayName and StartTlsServer summary say TLS upgrade available/unavailable
