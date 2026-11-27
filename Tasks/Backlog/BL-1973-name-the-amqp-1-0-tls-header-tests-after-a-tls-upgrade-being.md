---
id: BL-1973
title: Name the AMQP 1.0 TLS header tests after a TLS upgrade being available, not a certificate
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests/Amqp1SecurityNegotiationTests.cs, Documentation/Conformance/Extended/Amqp1.md]
requirement: none
created: 2026-11-27
completed:
---
# BL-1973 — Name the AMQP 1.0 TLS header tests after a TLS upgrade being available, not a certificate

## Goal

The names of the AMQP 1.0 TLS header tests say what they vary: whether a TLS upgrade is available.

## Context

Found while working BL-1952 (2026-11-27), which did the same for LDAP. `Amqp1SecurityNegotiation` is told only
`Amqp1SecurityNegotiationOptions.CanUpgradeToTls` (a certificate or TLS-SRP; ADR-0122 decision 5), never a
certificate. BL-1932 corrected the comments and conformance rows and left the test names.

- `Protocols/Messaging/Surl.Extended.Protocol.Amqp1.UnitTests/Amqp1SecurityNegotiationTests.cs`, lines 219 and 234
  (approximate): `NegotiateAsync_TlsHeaderWithACertificate_IsAnsweredUpgradedAndNegotiatedAgainInsideTls`,
  `NegotiateAsync_TlsHeaderWithoutACertificate_IsAnsweredWithTheSaslHeaderAndClosed`.
- `Documentation/Conformance/Extended/Amqp1.md`, rows `Amqp1:Security-5.2-1` and `Amqp1:Surl-Headers-2` (lines 43-44)
  cite them. Their requirement text is not changed.

Use the LDAP shape: `...WithTlsUpgradeAvailable_...`, `...WithTlsUpgradeUnavailable_...`. Names and cited names only;
no store, event or fault is involved.

## Acceptance criteria

- [ ] No test method name or `DisplayName` in `Amqp1SecurityNegotiationTests.cs` says "Certificate" or "certificate"; each renamed test says TLS upgrade available or unavailable.
- [ ] Rows `Amqp1:Security-5.2-1` and `Amqp1:Surl-Headers-2` cite the new test names, and every test they name exists in `Surl.Extended.Protocol.Amqp1.UnitTests`.
- [ ] The diff changes only those names and those citations.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
