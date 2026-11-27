---
id: BL-1959
title: Name the IMAP STARTTLS tests after a TLS upgrade being available, not a certificate
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Imap.UnitTests/ImapStartTlsTests.cs, Protocols/Mail/Surl.Protocol.Imap.UnitTests/RecordedFixtureTests.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-1959 — Name the IMAP STARTTLS tests after a TLS upgrade being available, not a certificate

## Goal

The test name and comment in the IMAP tests that vary the TLS upgrade say whether it is available.

## Context

Found while working BL-1952 (2026-11-27), which did the same for LDAP. `ImapProtocolServer` is told only
`isTlsUpgradeAvailable` (a certificate or TLS-SRP; ADR-0122 decision 5, name per ADR-0069), never a certificate. Both
files are in `Protocols/Mail/Surl.Protocol.Imap.UnitTests/`; line numbers are as of that date and approximate.

- `ImapStartTlsTests.cs`, line 47: `ServeAsync_StartTlsWithoutACertificate_AnswersNotAvailableAndStaysPlaintext`.
- `RecordedFixtureTests.cs`, line 147: the comment "against a server with no certificate" above
  `ServeAsync_RecordedStartTlsNotOffered_WritesTheRecordedResponses`. The recorded fixture files do not change.

Use the LDAP shape: `...WithTlsUpgradeUnavailable_...`. The class name stays (ADR-0069 decision 2). Names and
comments only; no store, event or fault is involved.

## Acceptance criteria

- [ ] No test method name in `ImapStartTlsTests.cs` says "Certificate"; the renamed test says TLS upgrade unavailable.
- [ ] The comment above `ServeAsync_RecordedStartTlsNotOffered_WritesTheRecordedResponses` says the server has no TLS upgrade available, not no certificate.
- [ ] The diff changes only that name and that comment.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
