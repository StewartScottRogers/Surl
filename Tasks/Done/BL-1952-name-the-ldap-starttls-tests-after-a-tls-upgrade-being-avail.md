---
id: BL-1952
title: Name the LDAP StartTLS tests after a TLS upgrade being available, not a certificate
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Protocols/Messaging/Surl.Protocol.Ldap.UnitTests/LdapProtocolServerStartTlsTests.cs]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-1952 — Name the LDAP StartTLS tests after a TLS upgrade being available, not a certificate

## Goal

The names in `LdapProtocolServerStartTlsTests` say what the tests vary: whether a TLS upgrade is available.

## Context

Found while working BL-1944 (2026-11-27), which corrected the class `<summary>` and was limited to `///` lines. The
server is told only `isTlsUpgradeAvailable` (a certificate or TLS-SRP), but in
`Protocols/Messaging/Surl.Protocol.Ldap.UnitTests/LdapProtocolServerStartTlsTests.cs` these still say "certificate":
`ServeAsync_StartTlsWithACertificate_AnswersSuccessThenUpgrades`,
`ServeAsync_StartTlsWithoutACertificate_IsProtocolErrorWithNoNameAndNoUpgrade`,
`ServeAsync_OtherExtendedOperationWithACertificate_IsProtocolErrorWithNoName`, and the two `DisplayName`s on
`ServeAsync_StartTlsOnATlsConnection_IsOperationsError`. Names only; no store, event or fault is involved.

## Acceptance criteria

- [x] No test method name or `DisplayName` in `LdapProtocolServerStartTlsTests.cs` says "Certificate" or "certificate"; each says TLS upgrade available or unavailable.
- [x] The diff changes only those names.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.

## Notes

- Renamed three test methods to `...WithTlsUpgradeAvailable_...` / `...WithTlsUpgradeUnavailable_...` and the two
  `DisplayName`s to "With a TLS upgrade available" / "With a TLS upgrade unavailable". No file outside `Tasks/` cited
  the old names.
- Verified 2026-11-27: `dotnet build --no-restore -warnaserror` 0 warnings, 0 errors; `dotnet format
  --verify-no-changes` passed; fast tests exit 0 with no failures. `Integration` tests not run: names only.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. The LDAP StartTLS test names and DisplayNames say TLS upgrade available or unavailable, not certificate; BL-1958 to BL-1963 filed for the same drift in other protocols.
