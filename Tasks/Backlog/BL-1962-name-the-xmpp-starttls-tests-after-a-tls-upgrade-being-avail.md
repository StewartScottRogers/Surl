---
id: BL-1962
title: Name the XMPP STARTTLS tests after a TLS upgrade being available, not a certificate
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests/XmppServerStartTlsTests.cs, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests/XmppResourceConflictTests.cs, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests/XmppServerOptionsTests.cs, Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests/XmppServerTests.cs, Documentation/Conformance/Extended/Xmpp.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1962 — Name the XMPP STARTTLS tests after a TLS upgrade being available, not a certificate

## Goal

The names and comments in the XMPP tests that vary the TLS upgrade say whether it is available.

## Context

Found while working BL-1952 (2026-11-27), which did the same for LDAP. `XmppServer` is told only
`XmppServerOptions.IsTlsUpgradeAvailable` (a certificate or TLS-SRP; ADR-0122 decision 5), never a certificate. Test
files are in `Protocols/Messaging/Surl.Extended.Protocol.Xmpp.UnitTests/`; line numbers are as of that date and
approximate.

- `XmppServerStartTlsTests.cs`, lines 43, 104 and 210: `ServeAsync_CertificateConfigured_OffersRequiredStartTlsAlone`,
  `ServeAsync_StartTlsWithoutACertificate_IsTheTlsFailureAndClose`,
  `ServeAsync_NoCertificateAndNoPlainTextMechanism_OffersNoFeatures`. The log note pinned at line 116 ("neither a
  certificate nor TLS-SRP is available") is true and stays (BL-1945).
- `XmppResourceConflictTests.cs`, line 222 and its callers: the helper `NoCertificateOptions`.
- `XmppServerOptionsTests.cs`, line 19: `Default_HasNoCertificateRefusesPlainTextAndLetsEveryoneIn`.
- `XmppServerTests.cs`, line 25: the comment "With no certificate, the plain-text features".
- `Documentation/Conformance/Extended/Xmpp.md`, rows `Xmpp:RFC6120-5.3.1-1` (line 50) and `Xmpp:RFC6120-5.4.2.2-1`
  (line 55) cite two of these tests. Their requirement text is not changed.

Use the LDAP shape: `...TlsUpgradeAvailable...`, `...TlsUpgradeUnavailable...`. Names, comments and cited names only;
no store, event or fault is involved.

## Acceptance criteria

- [ ] No test method or helper name in the four test files says "Certificate"; each says TLS upgrade available or unavailable.
- [ ] The `XmppServerTests` comment above `PlainFeatures` says no TLS upgrade is available, not no certificate.
- [ ] Rows `Xmpp:RFC6120-5.3.1-1` and `Xmpp:RFC6120-5.4.2.2-1` cite the new test names, and every test they name exists in `Surl.Extended.Protocol.Xmpp.UnitTests`.
- [ ] The diff changes only those names, that comment and those citations.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
