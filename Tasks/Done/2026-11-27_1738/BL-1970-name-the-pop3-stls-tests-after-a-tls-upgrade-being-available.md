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
completed: 2026-11-27
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

- [x] No test method name in `Pop3StlsTests.cs` says "Certificate"; the renamed test says TLS upgrade unavailable.
- [x] The comment above `ServeAsync_RecordedStlsNotOffered_WritesTheRecordedReplies` says the server has no TLS upgrade available, not no certificate.
- [x] The diff changes only that name and that comment.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Renamed to `ServeAsync_StlsWithTlsUpgradeUnavailable_IsNotAvailableAndNotAdvertised`; rewrapped the two-line
  comment. No other `.cs` or `.md` file outside this task named the old test.
- The two edits were made in the session rather than delegated to `align-and-document`: too small to be worth a
  subagent. Verified 2026-11-27: build 0 warnings, `dotnet format --verify-no-changes` clean, fast tests 0 failed
  (POP3: 247 passed). No integration tests run: no behaviour changed.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The POP3 STLS test name and recorded-fixture comment say the TLS upgrade is unavailable, not that there is no certificate.
