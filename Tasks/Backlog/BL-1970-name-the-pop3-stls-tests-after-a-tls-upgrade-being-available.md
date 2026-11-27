---
id: BL-1970
title: Name the POP3 STLS tests after a TLS upgrade being available, not a certificate
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Mail/Surl.Protocol.Pop3.UnitTests/Pop3StlsTests.cs, Protocols/Mail/Surl.Protocol.Pop3.UnitTests/RecordedFixtureTests.cs]
requirement: none
created: 2026-11-27
completed:
---
# BL-1970 — Name the POP3 STLS tests after a TLS upgrade being available, not a certificate

## Goal

The test name and comment in the POP3 tests that vary the TLS upgrade say whether it is available.

## Context

Found while working BL-1952 (2026-11-27), which did the same for LDAP. `Pop3ProtocolServer` is told only
`isTlsUpgradeAvailable` (a certificate or TLS-SRP; ADR-0122 decision 5, name per ADR-0069), never a certificate. Both
files are in `Protocols/Mail/Surl.Protocol.Pop3.UnitTests/`; line numbers are as of that date and approximate.

- `Pop3StlsTests.cs`, line 86: `ServeAsync_StlsWithoutACertificate_IsNotAvailableAndNotAdvertised`.
- `RecordedFixtureTests.cs`, line 105: the comment "against a server with no certificate" above
  `ServeAsync_RecordedStlsNotOffered_WritesTheRecordedReplies`. The recorded fixture files do not change.

Use the LDAP shape: `...WithTlsUpgradeUnavailable_...`. The class name stays (ADR-0069 decision 2). Names and
comments only; no store, event or fault is involved.

## Acceptance criteria

- [ ] No test method name in `Pop3StlsTests.cs` says "Certificate"; the renamed test says TLS upgrade unavailable.
- [ ] The comment above `ServeAsync_RecordedStlsNotOffered_WritesTheRecordedReplies` says the server has no TLS upgrade available, not no certificate.
- [ ] The diff changes only that name and that comment.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

## Log

- 2026-11-27: Created.
