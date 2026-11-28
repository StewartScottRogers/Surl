---
id: BL-2004
title: Judge an LDAP directory user's OCI and MCP tokens as the amended ADR-0167 decides
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2003, BL-2018]
touches: [Foundation/Surl.Authentication.UnitLibrary, Foundation/Surl.Authentication.UnitTests]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-2004 — Judge an LDAP directory user's OCI and MCP tokens as the amended ADR-0167 decides

## Goal

`AuthenticationPolicy` answers the OCI and MCP per-use token check for an LDAP directory user as ADR-0167 Amendment 3
(BL-2003) decides.

## Context

Found while working BL-1981 (2026-11-27), read from source. With `--user-ldap` serving `oci` or `mcp`,
`AuthenticationPolicy.CheckPasswordLoginAsync` accepts a directory user and a token is issued to the name, but
`AuthenticationPolicy.NamesAccount` is `AccountBook.HasNamedAccount` alone, so `OciAccessCheck` and
`McpAuthorizationGrants` refuse that token as unknown.

Read Amendment 3 first: it fixes the behaviour, the member and the libraries. In short: `AuthenticationPolicy`
implements `IAuthenticationPolicy.HonoursTokenIssuedTo` (added by BL-2018), true when `NamesAccount` is, or when this
policy's `CheckPasswordLoginAsync` accepted the name through the directory (no fault refusing it) less than one hour
ago on the injected `TimeProvider` and the account book has never held the name since the instance started. At most
10000 names are recorded. `NamesAccount` does not change (point 5). The OCI and MCP servers call the new member in
BL-2019 and BL-2020. Tests drive the directory in memory through `ILdapDirectoryConnector`, on the settings view the
server's policy is composed with (`AuthenticationSettings.ForScheme`). No socket, no `Integration` test, no upstream
curl measurement (curl has neither scheme).

Stand-in: no new store, event or fault. The password login keeps publishing `LoginJudgedEvent` and taking
`refuse-login`; a test takes a book account's access away with `remove_account`, reset or restore, and a directory
user's tokens with a sessions reset.

## Acceptance criteria

- [x] A test in `Foundation/Surl.Authentication.UnitTests` logs a directory-only user in by `CheckPasswordLoginAsync` on the `oci` view and asserts the amendment's answer to the token check; it was run and seen failing before the production change (say so in Notes), and passes after.
- [x] A test does the same for `mcp`, and one for the TLS twin `mcps`.
- [x] Each case the amendment lists as refused has a passing test, a removed account-book account whose name the directory also holds among them, before and after the directory accepts that name; `NamesAccount` stays `false` for a directory user.
- [x] Tests over a fake `TimeProvider` show the check true just before one hour after the accepted login, false at one hour, true again after another accepted login but not renewed by a refused one; and, past 10000 recorded names, the name with the oldest login dropped.
- [x] The XML comments of every member changed, and the library's `CLAUDE.md` on `NamesAccount`, `HonoursTokenIssuedTo` and the LDAP account source, state the amended rule.
- [x] `dotnet build Foundation/Surl.Authentication.UnitLibrary -warnaserror` is clean, the fast tests pass, and the library stays at 100% line and branch coverage.

## Notes

- Plan: ADR-0167 Amendment 3 fixes behaviour, member and libraries, so no new ADR and no planning subagent; the change
  was built and reviewed in this session. No upstream curl measurement (curl has neither scheme).
- Seen failing first: with only `LdapDirectoryUserTokenPolicyTests` added, 8 of its 25 cases failed, the `oci`, `mcp`
  and `mcps` cases among them (the interface default answered `false`). All pass after the change.
- Design: a new internal `AcceptedDirectoryLogins` (one per policy) holds name and time under a lock;
  `AccountStore` keeps every name it has held. Only `CheckPasswordLoginAsync` records, as the amendment says.
- Choice: the hour is read from `TimeProvider.GetUtcNow`, not the monotonic timestamp, so a test clock that
  overrides only `GetUtcNow` moves it, as it moves this library's other expiries.
- Gates (2026-11-27): `dotnet build -warnaserror` clean, `dotnet format --verify-no-changes` exit 0, fast tests
  30470 passed, 0 failed, 3 skipped; `Measure-CodeQuality.ps1 -Library Surl.Authentication.UnitLibrary`: 0 failing members.

## Log

- 2026-11-27: Created.
- 2026-11-27: Now depends on BL-2018 too; Context and criteria matched to ADR-0167 Amendment 3 (BL-2003).
- 2026-11-27: Backlog -> Doing.
- 2026-11-27: Doing -> Done. AuthenticationPolicy honours an LDAP directory user's token for one hour after an accepted password login, refusing names the account book has ever held
